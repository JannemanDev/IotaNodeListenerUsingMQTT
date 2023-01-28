using Bech32Lib;
using MQTTClient;
using MQTTClient.Configuration;
using MQTTClient.Extensions;
using MQTTHornetNode.Models.Api.Messages;
using MQTTHornetNode.Models.Api.Outputs;
using MQTTHornetNode.Models.Api.Transactions;
using MQTTHornetNode.Models.MqttEvents;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Subscribing;
using MQTTnet.Extensions;
using MQTTnet.Extensions.WebSocket4Net;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Input = MQTTHornetNode.Models.Api.Transactions.Input;
using Output = MQTTHornetNode.Models.Api.Transactions.Output;

namespace MQTTHornetNode
{
    internal class MqttClientListener
    {
        // key = txId, value = spent(s) and unspent(s) MQTT event(s) with additional data
        private Dictionary<string, MonitoredWalletEventsForTransaction> monitoredWalletEventsForTransactions = new Dictionary<string, MonitoredWalletEventsForTransaction>();
        private List<TransactionForMonitoredWallet> transactionsForMonitoredWallets;

        private readonly IotaNodeService iotaNodeService;
        private readonly PushOverService pushOverService;
        public IMqttClient MqttClient { get; private set; }
        private readonly Settings settings;
        private readonly List<MonitorWalletAddress> enabledMonitorWalletAddresses;

        private bool SendNotificationForDisconnect = false;
        private string nameInstance;

        public Dictionary<string, long> CountPerledgerInclusionState { get; private set; }
        public Dictionary<string, long> CountPerValueTxIndex { get; private set; }
        public Dictionary<string, long> TotalAmountPerValueTxIndex { get; private set; }

        private DateTime? disconnectedAt;

        public MqttClientListener(string nameInstance, PushOverService pushOverService, IotaNodeService iotaNodeService, Settings settings, MonitorWalletAddress[] monitorWalletAddresses)
        {
            if (pushOverService == null || iotaNodeService == null) throw new ArgumentNullException($"pushOverService {pushOverService} and iotaNodeService {iotaNodeService} can not be null!");

            this.nameInstance = nameInstance;
            this.pushOverService = pushOverService;
            this.iotaNodeService = iotaNodeService;
            this.settings = settings;
            this.enabledMonitorWalletAddresses = monitorWalletAddresses
                .Where(mwa => mwa.Enabled)
                .ToList();

            CountPerledgerInclusionState = new Dictionary<string, long>();
            CountPerValueTxIndex = new Dictionary<string, long>();
            TotalAmountPerValueTxIndex = new Dictionary<string, long>();

            transactionsForMonitoredWallets = LoadTransactionsForMonitoredWallets();

            Task task = ConnectAsync();
            task.GetAwaiter().GetResult();
        }

        private async Task ConnectAsync()
        {
            var factory = new MqttFactory();
            if (settings.Mqtt.UseWebSocket4Net) MqttClient = factory.UseWebSocket4Net().CreateMqttClient();
            else MqttClient = factory.CreateMqttClient();

            MqttClient.UseConnectedHandler(async e =>
            {
                Log.Logger.Information("### CONNECTED WITH SERVER ###");

                if (SendNotificationForDisconnect)
                {
                    string s = "";
                    if (disconnectedAt != null)
                    {
                        TimeSpan elapsed = (TimeSpan)(DateTime.Now - disconnectedAt);
                        Log.Logger.Information($"{DateTime.Now} {disconnectedAt} {elapsed} {elapsed.ConvertToFriendlyString()}");
                        s = $"after {elapsed.ConvertToFriendlyString()} ";
                        disconnectedAt = null;
                    }
                    await pushOverService.SendNotificationsAsync($"{nameInstance} reconnected!", $"{nameInstance} is successfully reconnected {s}to IOTA node at {settings.Mqtt.Uri}!", GetAllIncludedGroupAndUserKeysForWallets(enabledMonitorWalletAddresses));
                    SendNotificationForDisconnect = false;
                }

                if (settings.Mqtt.Topics.Any())
                {
                    //1. subscribe to specified topics
                    foreach (string topic in settings.Mqtt.Topics)
                    {
                        if (await SubscribeToTopic(MqttClient, topic)) Log.Logger.Information($"Successfully subscribed to: {topic}");
                    };

                    //2. subscribe for each enabled wallet to be monitored
                    foreach (MonitorWalletAddress monitorWalletAddress in enabledMonitorWalletAddresses)
                    {
                        string topic = $"addresses/{monitorWalletAddress.WalletAddress}/outputs";
                        string desc = monitorWalletAddress.Description;
                        if (await SubscribeToTopic(MqttClient, topic)) Log.Logger.Information($"Successfully subscribed to: {desc}: {topic}");
                    };

                    MqttClient.UseApplicationMessageReceivedHandler(e => HandleEventMessageAsync(e));

                    Log.Logger.Information("");
                }
            });

            MqttClient.UseDisconnectedHandler(async e =>
            {
                //if (e.Reason == MQTTnet.Client.Disconnecting.MqttClientDisconnectReason.NormalDisconnection) return;
                if (disconnectedAt == null) disconnectedAt = DateTime.Now;

                Log.Logger.Error($"Disconnected! Reason: \"{e.Reason}\"");

                if (!SendNotificationForDisconnect)
                {
                    await pushOverService.SendNotificationsAsync($"{nameInstance} offline!", $"{nameInstance} is disconnected from IOTA node at {settings.Mqtt.Uri}! Will retry until connected. Notification will be sent once reconnected.", GetAllIncludedGroupAndUserKeysForWallets(enabledMonitorWalletAddresses));
                    SendNotificationForDisconnect = true;
                }

                Log.Logger.Information("### DISCONNECTED FROM SERVER ###");
                await Task.Delay(TimeSpan.FromSeconds(5));

                try
                {
                    Log.Logger.Information("### RETRYING TO CONNECT ###");
                    await ConnectMqttClientAsync(MqttClient, settings.Mqtt);
                }
                catch
                {
                    Log.Logger.Information("### RECONNECTING FAILED ###");
                }
            });

            await ConnectMqttClientAsync(MqttClient, settings.Mqtt);
        }

        private async Task ConnectMqttClientAsync(IMqttClient mqttClient, Mqtt mqttSettings)
        {
            string clientId;
            if (mqttSettings.ClientId != null && mqttSettings.ClientId != "") clientId = mqttSettings.ClientId;
            else clientId = Guid.NewGuid().ToString();

            MqttClientOptionsBuilderTlsParameters mcobtp = new MqttClientOptionsBuilderTlsParameters();
            mcobtp.SslProtocol = mqttSettings.SslProtocolAsEnum;
            mcobtp.UseTls = true;
            mcobtp.IgnoreCertificateChainErrors = true;
            mcobtp.IgnoreCertificateRevocationErrors = true;
            mcobtp.AllowUntrustedCertificates = true;

            mcobtp.CertificateValidationHandler = (certContext) =>
            {
                string output;
                output = JsonConvert.SerializeObject(certContext.Certificate, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());
                Log.Logger.Debug($"X509Certificate: {output}\n");

                output = JsonConvert.SerializeObject(certContext.Chain, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());
                Log.Logger.Debug($"X509Chain: {output}\n");

                output = JsonConvert.SerializeObject(certContext.SslPolicyErrors, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());
                Log.Logger.Information($"SslPolicyErrors: {output}\n");

                //return false if any error
                return certContext.SslPolicyErrors == SslPolicyErrors.None;
            };

            MqttClientOptionsBuilderWebSocketParameters mqttClientOptionsBuilderWebSocketParameters = new MqttClientOptionsBuilderWebSocketParameters();
            mqttClientOptionsBuilderWebSocketParameters.RequestHeaders = mqttSettings.WebSocketParameters.RequestHeaders;
            if (!settings.Mqtt.UseWebSocket4Net)
            {
                mqttClientOptionsBuilderWebSocketParameters.CookieContainer = new CookieContainer();
                mqttClientOptionsBuilderWebSocketParameters.CookieContainer.Add(mqttSettings.WebSocketParameters.CookieCollection);
            }
            else if (mqttSettings.WebSocketParameters.CookieCollection.Any())
            {
                Log.Logger.Warning($"{mqttSettings.WebSocketParameters.CookieCollection.Count} cookies defined in settings but ignored, because not supported when using UseWebSocket4Net");
            }

            MqttClientCredentials mcc = new MqttClientCredentials();
            mcc.Username = mqttSettings.Username;
            mcc.Password = Encoding.ASCII.GetBytes(mqttSettings.Password);

            MqttClientOptionsBuilder mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithCredentials(mcc)
                .WithCleanSession(mqttSettings.CleanSession)
                .WithProtocolVersion(mqttSettings.ProtocolVersionAsEnum);

            Uri uri = new Uri(mqttSettings.Uri);
            string scheme = uri.Scheme.ToLower();
            if (scheme == "ws") mqttClientOptionsBuilder.WithWebSocketServer(mqttSettings.Uri, mqttClientOptionsBuilderWebSocketParameters);
            else if (scheme == "wss") mqttClientOptionsBuilder.WithWebSocketServer(mqttSettings.Uri, mqttClientOptionsBuilderWebSocketParameters).WithTls(mcobtp);
            else if (scheme == "tcp" || scheme == "mqtt") mqttClientOptionsBuilder.WithTcpServer(uri.Host, uri.Port);
            else if (scheme == "mqtts") mqttClientOptionsBuilder.WithTcpServer(uri.Host, uri.Port).WithTls(mcobtp);
            else throw new ArgumentException($"Unexpected scheme in uri {uri.AsJson()}");

            IMqttClientOptions mqttClientOptions = mqttClientOptionsBuilder.Build();

            await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
        }

        private static async Task<bool> SubscribeToTopic(IMqttClient mqttClient, string topic)
        {
            try
            {
                MqttClientSubscribeOptions mqttClientSubscribeOptions = new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(topic, MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                    .Build();

                await mqttClient.SubscribeAsync(mqttClientSubscribeOptions);
                return true;
            }
            catch (Exception e)
            {
                Log.Logger.Error($"Error while subscribing to {topic}: {e.Message} - {e.InnerException?.Message}");
                return false;
            }
        }

        private async Task HandleEventMessageAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            Log.Logger.Information("### RECEIVED MQTT EVENT MESSAGE ###");
            string topic = e.ApplicationMessage.Topic.ToLower();
            Log.Logger.Information($"+ Topic = {topic}");
            string payload;

            if (topic == "messages" || topic.StartsWith("transactions/"))
            {
                //binary -> display as base64
                payload = Convert.ToBase64String(e.ApplicationMessage.Payload);
            }
            else payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload).BeautifyJson();

            Log.Logger.Information($"+ Payload =\n{payload}");
            Log.Logger.Debug($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
            Log.Logger.Debug($"+ Retain = {e.ApplicationMessage.Retain}");

            if (topic.StartsWith("milestones/"))
            {
                if (topic.EndsWith("/confirmed"))
                {
                    //Log.Logger.Information($"MonitoredWalletEventsForTransactions: {monitoredWalletEventsForTransactions.AsJson()}");

                    List<KeyValuePair<string, MonitoredWalletEventsForTransaction>> unprocessedMonitoredWalletEventsForTransactions = monitoredWalletEventsForTransactions
                                                                                                                                        .Where(x => !x.Value.Processed).ToList();
                    if (unprocessedMonitoredWalletEventsForTransactions.Any())
                    {
                        Log.Logger.Information($"UnprocessedMonitoredWalletEventsForTransactions ({unprocessedMonitoredWalletEventsForTransactions.Count}x): {unprocessedMonitoredWalletEventsForTransactions.AsJson()}");

                        for (int j = 0; j < unprocessedMonitoredWalletEventsForTransactions.Count; j++)
                        {
                            KeyValuePair<string, MonitoredWalletEventsForTransaction> unprocessedMonitoredWalletEventsForTransaction = unprocessedMonitoredWalletEventsForTransactions[j];
                            string txIdDic = unprocessedMonitoredWalletEventsForTransaction.Key;
                            string txId = unprocessedMonitoredWalletEventsForTransaction.Value.TxId;
                            if (txIdDic != txId) throw new Exception($"TxId of monitored wallet events {txId} is different than the key that was used to store it in the dictionary: {txIdDic}");

                            //Todo: save message id for link generation
                            Transaction tx = iotaNodeService.GetTransactionDetails(txId);

                            TransactionForMonitoredWallet transactionForMonitoredWallet = new TransactionForMonitoredWallet(txId);

                            Log.Logger.Information($"Checking inputs of txId {txId}:\n{tx.AsJson()}");

                            //Check if there are tx *input(s)* not found in MQTT *spent* event(s)
                            for (int i = 0; i < tx.Data.Payload.Essence.Inputs.Count; i++)
                            {
                                Input input = tx.Data.Payload.Essence.Inputs[i];
                                MonitoredWalletEvent monitoredWalletEventFound = unprocessedMonitoredWalletEventsForTransaction.Value.MonitoredWalletEvents
                                    .Find(mwe => mwe.IsSpent == true && mwe.UtxoId.Equals(input.UtxoId, StringComparison.OrdinalIgnoreCase));

                                if (monitoredWalletEventFound != null)
                                { //exists
                                    Log.Logger.Information($"Input\n{input.AsJson()}\nfound in MonitoredWalletEvents:\n{monitoredWalletEventFound.AsJson()}");
                                    transactionForMonitoredWallet.AddMonitoredWalletEvent(monitoredWalletEventFound);
                                }
                                else //does not exist
                                {
                                    Log.Logger.Information($"Input\n{input.AsJson()}\nNOT found in MonitoredWalletEvents\n{unprocessedMonitoredWalletEventsForTransaction.Value.MonitoredWalletEvents.AsJson()}");

                                    Utxo utxo = iotaNodeService.GetUtxoDetails(input.UtxoId);
                                    transactionForMonitoredWallet.AddUtxo(utxo);
                                }
                            }

                            Log.Logger.Information($"Checking outputs of txId {txId}:\n{tx.AsJson()}");

                            //Check if there are tx *output(s)* not found in MQTT *unspent* event(s)
                            for (int i = 0; i < tx.Data.Payload.Essence.Outputs.Count; i++)
                            {
                                Output output = tx.Data.Payload.Essence.Outputs[i];
                                MonitoredWalletEvent monitoredWalletEventFound = unprocessedMonitoredWalletEventsForTransaction.Value.MonitoredWalletEvents
                                    .Find(mwe => mwe.IsSpent == false && mwe.OutputIndex == i);

                                if (monitoredWalletEventFound != null)
                                { //exists
                                    Log.Logger.Information($"Output\n{output.AsJson()}\nfound in MonitoredWalletEvents:\n{monitoredWalletEventFound.AsJson()}");

                                    transactionForMonitoredWallet.AddMonitoredWalletEvent(monitoredWalletEventFound);

                                    //double check if amount and Bech32Address are the same
                                    if (monitoredWalletEventFound.Amount != output.Amount || monitoredWalletEventFound.Bech32Address != output.Address.Bech32Address)
                                        throw new Exception($"Amount and address of {monitoredWalletEventFound.AsJson()} is NOT the same as {output.AsJson()}");
                                }
                                else //does not exist
                                {
                                    Log.Logger.Information($"Output\n{output.AsJson()}\nNOT found in MonitoredWalletEvents\n{unprocessedMonitoredWalletEventsForTransaction.Value.MonitoredWalletEvents.AsJson()}");

                                    TxPart txPart = new TxPart() { Amount = output.Amount, Bech32Address = output.Address.Bech32Address, IsInput = false };
                                    transactionForMonitoredWallet.AddTxPart(txPart);
                                }
                            }

                            unprocessedMonitoredWalletEventsForTransaction.Value.Processed = true;
                            transactionsForMonitoredWallets.Add(transactionForMonitoredWallet);
                            //Log.Logger.Information($"transactionsForMonitoredWallet: {transactionsForMonitoredWallets.AsJson()}");

                            SaveTransactionsForMonitoredWallets();

                            //send notifications
                            await SendNotificationsForTransactionsForMonitoredWalletsAsync(transactionsForMonitoredWallets);

                            SaveTransactionsForMonitoredWallets();
                        }
                    }
                }
            }

            if (topic.StartsWith("addresses/") && topic.EndsWith("/outputs"))
            {
                string walletAddressFromTopic = topic.TrimStart("addresses/").TrimEnd("/outputs");

                AddressesOutputs addressesOutputs = JsonConvert.DeserializeObject<AddressesOutputs>(payload);
                Log.Logger.Information($"WalletAddressFromTopic: {walletAddressFromTopic}");
                Log.Logger.Information($"AddressesOutputs: {addressesOutputs.AsJson()}");

                string txId;
                string txIdUtxo;
                long amount = addressesOutputs.Output.Amount;
                int outputIndex = addressesOutputs.OutputIndex;
                string bech32Address = walletAddressFromTopic;
                string outputBech32Address = Bech32.Encode("iota", addressesOutputs.Output.Address.AddressAddress);
                if (bech32Address != outputBech32Address) throw new Exception($"Monitored walletaddress for this event is {bech32Address} but it is NOT the same as output address {outputBech32Address}");

                if (addressesOutputs.IsSpent)
                {
                    Utxo utxo = iotaNodeService.GetUtxoDetails(addressesOutputs.UtxoId);
                    txId = utxo.Data.TransactionIdSpent;
                    txIdUtxo = addressesOutputs.TransactionId;
                }
                else
                {
                    txId = addressesOutputs.TransactionId;
                    txIdUtxo = txId;
                }

                //Transaction transaction = iotaNodeService.GetTransactionDetails(txId);

                MonitoredWalletEvent monitoredWalletEvent = new MonitoredWalletEvent(bech32Address, txIdUtxo, outputIndex, amount, addressesOutputs.IsSpent);
                MonitoredWalletEventsForTransaction monitoredWalletEventsForTransaction;
                if (!monitoredWalletEventsForTransactions.TryGetValue(txId, out monitoredWalletEventsForTransaction))
                {
                    monitoredWalletEventsForTransaction = new MonitoredWalletEventsForTransaction(txId, monitoredWalletEvent);
                }
                monitoredWalletEventsForTransaction.MonitoredWalletEvents.Add(monitoredWalletEvent);
                monitoredWalletEventsForTransactions.Add(txId, monitoredWalletEventsForTransaction);
            }

            if (topic == "messages/referenced")
            {
                dynamic payloadObj = JsonConvert.DeserializeObject(payload);

                string ledgerInclusionState = payloadObj["ledgerInclusionState"];

                long countForLedgerInclusionState = 0;
                CountPerledgerInclusionState.TryGetValue(ledgerInclusionState, out countForLedgerInclusionState);
                CountPerledgerInclusionState[ledgerInclusionState] = countForLedgerInclusionState + 1;

                string messageId = payloadObj["messageId"];
                if (ledgerInclusionState.ToLower() == "included")
                {
                    Message message = iotaNodeService.GetMessageDetails(messageId);
                    TransactionMessage transactionMessage = new TransactionMessage(messageId, message);

                    long amountIotas = transactionMessage.Message.Data.Payload.Essence.Outputs.Sum(output => output.Amount);

                    string s = $"Included tx: {messageId} with amount {amountIotas.ConvertToBiggestUnit(out _, out _)}";

                    EssencePayload essencePayload = message.Data.Payload.Essence.Payload;
                    if (essencePayload != null)
                    {
                        string indexAsStr = essencePayload.Index.HexStringToString();
                        s = $"{s} and index {indexAsStr}";

                        long countForValueTxIndex = 0;
                        CountPerValueTxIndex.TryGetValue(indexAsStr, out countForValueTxIndex);
                        CountPerValueTxIndex[indexAsStr] = countForValueTxIndex + 1;

                        long sumAmountForValueTxIndex = 0;
                        TotalAmountPerValueTxIndex.TryGetValue(indexAsStr, out sumAmountForValueTxIndex);
                        TotalAmountPerValueTxIndex[indexAsStr] = sumAmountForValueTxIndex + amountIotas;

                        string essencePayloadData = essencePayload.Data.HexStringToString();
                        if (essencePayloadData != "")
                        {
                            if ((essencePayloadData.Trim().StartsWith("{")) && (essencePayloadData.Trim().EndsWith("}")))
                                essencePayloadData = essencePayloadData.BeautifyJson();
                            s = $"{s} and data{Environment.NewLine}{essencePayloadData}";
                        }
                    }
                    Log.Logger.Information(s);

                    Log.Logger.Debug($"{Settings.TxTag}{Environment.NewLine}{transactionMessage}");

                    if (ledgerInclusionState.ToLower() == "conflicting")
                    {
                        Log.Logger.Information($"Conflicting tx: {messageId}");
                    }
                }
            }
        }

        private void SaveTransactionsForMonitoredWallets()
        {
            //Save all transactions for monitored wallets
            string filename = Path.Combine(Program.ApplicationPath, "Logs", "TransactionsForMonitoredWallets.json");
            Log.Information($"Saving TransactionsForMonitoredWallets to: {filename}");
            System.IO.File.WriteAllText(filename, transactionsForMonitoredWallets.AsJson());
        }


        private List<TransactionForMonitoredWallet> LoadTransactionsForMonitoredWallets()
        {
            List<TransactionForMonitoredWallet> result = new List<TransactionForMonitoredWallet>();

            //Save all transactions for monitored wallets
            string filename = Path.Combine(Program.ApplicationPath, "Logs", "TransactionsForMonitoredWallets.json");
            if (System.IO.File.Exists(filename))
            {
                string json = System.IO.File.ReadAllText(filename);
                result = JsonConvert.DeserializeObject<List<TransactionForMonitoredWallet>>(json);
            }

            return result;
        }

        public async Task SendNotificationsForTransactionsForMonitoredWalletsAsync(List<TransactionForMonitoredWallet> transactionForMonitoredWallets)
        {
            //only consider not processed ones
            foreach (TransactionForMonitoredWallet transactionForMonitoredWallet in transactionForMonitoredWallets.Where(a => !a.Processed))
            {
                //a transactionForMonitoredWallet can contain 1 or more monitored wallet addresses !

                List<string> triggeredMonitorWalletAddresses = transactionForMonitoredWallet.TxParts
                    .Where(txPart => enabledMonitorWalletAddresses.Select(mwa => mwa.WalletAddress).Contains(txPart.Bech32Address))
                    .Select(txPart => txPart.Bech32Address)
                    .Distinct()
                    .ToList();

                //send notification for each triggeredMonitorWalletAddresses
                foreach (string triggeredMonitorWalletAddress in triggeredMonitorWalletAddresses)
                {
                    //which monitor wallet address?
                    MonitorWalletAddress mwa = enabledMonitorWalletAddresses.Single(mwa => mwa.WalletAddress == triggeredMonitorWalletAddress);

                    List<TxPart> txPartsOfTriggeredMonitorWalletAddress = transactionForMonitoredWallet.TxParts
                        .Where(txPart => txPart.Bech32Address.Equals(triggeredMonitorWalletAddress, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    Log.Logger.Information($"txPartsOfTriggeredMonitorWalletAddress for {triggeredMonitorWalletAddress}:\n{txPartsOfTriggeredMonitorWalletAddress.AsJson()}");

                    long inputAmount = 0;
                    long outputAmount = 0;
                    long totalAmount = 0;
                    inputAmount -= txPartsOfTriggeredMonitorWalletAddress.Where(a => a.IsInput).SumOrDefault(a => a.Amount);
                    outputAmount += txPartsOfTriggeredMonitorWalletAddress.Where(a => a.IsOutput).SumOrDefault(a => a.Amount);
                    totalAmount = inputAmount + outputAmount;

                    Log.Logger.Information($"inputAmount={inputAmount} outputAmount={outputAmount} totalAmount={totalAmount}");

                    string sentOrReceived = totalAmount < 0 ? "Sent" : "Received";
                    totalAmount.ConvertToBiggestUnit(out decimal totalAmountAsDecimal, out string totalAmountAsStr);
                    Log.Logger.Information($"totalAmountAsDecimal={totalAmountAsDecimal}");

                    string s = $"Change detected on wallet <b>{mwa.Description}</b> with address <b>{triggeredMonitorWalletAddress}</b>:\n{sentOrReceived} an amount of {totalAmountAsDecimal:0.##}{totalAmountAsStr} !\n";
                    s += $"\n";
                    s += $"<a href=\"https://explorer.iota.org/mainnet/message/{transactionForMonitoredWallet.TxId}\">IOTA Explorer</a>\n";
                    s += $"\n<i>{Program.RunningOnMachineSince}</i>";

                    Log.Logger.Information(s);
                    await pushOverService.SendNotificationsAsync($"{nameInstance} alert!", s, GetAllGroupAndUserKeysForWallet(mwa));
                }

                transactionForMonitoredWallet.Processed = true;
            }
        }

        private List<string> GetAllIncludedGroupAndUserKeysForWallets(List<MonitorWalletAddress> mwas)
        {
            List<string> result = new List<string>(settings.PushOver.GroupOrUserKeys); //make a copy !

            foreach (var mwa in mwas)
            {
                //include users which are defined for the triggeredMonitorWalletAddress
                result.AddRange(mwa.IncludeGroupOrUserKeys);
            }

            //removes any duplicates
            result = result.Distinct().ToList();

            return result;
        }

        private List<string> GetAllGroupAndUserKeysForWallet(MonitorWalletAddress mwa)
        {
            List<string> result = new List<string>(settings.PushOver.GroupOrUserKeys); //make a copy !

            Log.Logger.Debug($"settings.PushOver.GroupOrUserKeys:\n{result.AsJson()}");

            //include users which are defined for the triggeredMonitorWalletAddress
            result.AddRange(mwa.IncludeGroupOrUserKeys);

            Log.Logger.Debug($"AddRange:\n{result.AsJson()}");

            //removes any duplicates
            result = result.Distinct().ToList();
            Log.Logger.Debug($"Distinct:\n{result.AsJson()}");

            //exclude users which are defined for the triggeredMonitorWalletAddress
            result = result.Except(mwa.ExcludeGroupOrUserKeys).ToList();
            Log.Logger.Debug($"Except:\n{result.AsJson()}");

            return result;
        }
    }
}
