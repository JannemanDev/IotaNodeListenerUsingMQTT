using MQTTClient.Configuration;
using MQTTClient.Extensions;
using MQTTHornetNode.Models;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Subscribing;
using Newtonsoft.Json;
using Polly;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MQTTClient
{

    class Program
    {
        static Settings settings;
        static IMqttClient mqttClient;
        static Dictionary<string, long> countPerledgerInclusionState = new Dictionary<string, long>();
        static Dictionary<string, long> countPerValueTxIndex = new Dictionary<string, long>();
        static Dictionary<string, long> totalAmountPerValueTxIndex = new Dictionary<string, long>();
        static RestClient restClient;
        static readonly string TxTag = "[Tx]";

        static DateTime startedMonitoringAt;
        static Timer timer;

        static async Task Main(string[] args)
        {
            DefaultLogging();

            string settingsFile = ParseArguments(args);
            settings = LoadSettings(settingsFile);

            InitLogging();

            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();

            Log.Logger.Information("IotaNodeListenerUsingMQTT v0.1");

            MqttClientConnectResult mqttClientConnectResult = await ConnectMqttClientAsync(mqttClient, settings);

            restClient = InitRestClient();

            if (!settings.Application.RunAsService)
            {
                Log.Logger.Information("Use [escape] to quit");
                Log.Logger.Information("Use [spacebar] to send a testmessage\n");
                Log.Logger.Information("Use [I] to print info about all received messages\n");
            }
            else
            {
                Log.Logger.Information("Running as a service. Press CTRL-C to quit...");
            }

            startedMonitoringAt = DateTime.Now;

            if (settings.Application.RunAsService) InitTimer();

            while (true)
            {
                if (settings.Application.RunAsService)
                {
                    await Task.Delay(1000);
                }
                else
                {
                    if (System.Console.KeyAvailable)
                    {
                        ConsoleKeyInfo cki = System.Console.ReadKey(true);

                        //Quit
                        if (cki.Key == ConsoleKey.Escape)
                        {
                            await mqttClient.DisconnectAsync();

                            Environment.Exit(0);
                        }

                        //Info
                        if (cki.Key == ConsoleKey.I) PrintInfo();

                        //Send
                        if (cki.Key == ConsoleKey.Spacebar && settings.Mqtt.Topics.Any())
                        {
                            string message = "{ \"msg\": \"test\" }";
                            string topic = settings.Mqtt.Topics[0]; //using the first topic as example
                            await mqttClient.PublishAsync(topic, message, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce, false);
                            Log.Logger.Information($"Publishing \"{message}\" to {settings.Mqtt.Host}:{settings.Mqtt.Port} in topic \"{topic}\"");
                        }
                    }
                }
            }
        }

        private static void InitTimer()
        {
            int seconds = settings.Application.PrintTxsInfoEveryXMinutes;

            Log.Logger.Information($"Setting timer to {seconds} minute(s)");

            timer = new Timer(async (object state) => PrintInfo(), null, 0, (int)TimeSpan.FromMinutes(seconds).TotalMilliseconds);
        }

        private static void PrintInfo()
        {
            Log.Logger.Information($"Running time: {(DateTime.Now - startedMonitoringAt).ToString(@"dd\.hh\:mm\:ss")}");

            Log.Logger.Information("Count per ledger inclusion state:");

            countPerledgerInclusionState.ToList()
                .OrderBy(messageCountPerType => messageCountPerType.Key) //sort on state
                .ToList()
                .ForEach(messageCountForType => Log.Logger.Information($" {messageCountForType.Key,13} ={messageCountForType.Value,7}x"));

            Log.Logger.Information("Count per index of a value transaction:");

            int longestString = countPerValueTxIndex.ToList().MaxOrDefault(a => a.Key.Length);

            countPerValueTxIndex.ToList()
                .OrderBy(countPerIndex => countPerIndex.Key) //sort on index
                .ToList()
                .ForEach(countForIndex =>
                {
                    string s = countForIndex.Key.ToString().PadLeft(longestString);
                    Log.Logger.Information($" {s} ={countForIndex.Value,4}x");
                });

            Log.Logger.Information("Total amount of value transferred per index:");

            longestString = totalAmountPerValueTxIndex.ToList().MaxOrDefault(a => a.Key.Length);

            totalAmountPerValueTxIndex.ToList()
                .OrderBy(totalAmountPerIndex => totalAmountPerIndex.Key) //sort on totalAmount
                .ToList()
                .ForEach(totalAmountForIndex =>
                {
                    string s = totalAmountForIndex.Key.ToString().PadLeft(longestString);
                    Log.Logger.Information($" {s} ={totalAmountForIndex.Value.ConvertToBigestUnit2(out _, out _),10}");
                });
        }

        private static void DefaultLogging()
        {
            //Create default minimal logger until settings are loaded
            Log.Logger = new LoggerConfiguration()
             .MinimumLevel.Verbose() //send all events to sinks
             .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug)
             .CreateLogger();
        }

        private static async Task<MqttClientConnectResult> ConnectMqttClientAsync(IMqttClient mqttClient, Settings mqttSettings)
        {
            MqttClientOptionsBuilder mqttClientOptionsBuilder = new MqttClientOptionsBuilder();

            string clientId;
            if (mqttSettings.Mqtt.ClientId != null && mqttSettings.Mqtt.ClientId != "") clientId = mqttSettings.Mqtt.ClientId;
            else clientId = Guid.NewGuid().ToString();

            MqttClientOptionsBuilderTlsParameters mcobtp = new MqttClientOptionsBuilderTlsParameters();
            mcobtp.SslProtocol = mqttSettings.Mqtt.SslProtocolAsEnum;
            mcobtp.UseTls = mqttSettings.Mqtt.UseTls;
            mcobtp.CertificateValidationHandler = (certContext) =>
            {
                string output;
                output = JsonConvert.SerializeObject(certContext.Certificate, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());
                Log.Logger.Debug($"X509Certificate: {output}\n");

                output = JsonConvert.SerializeObject(certContext.Chain, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());
                Log.Logger.Debug($"X509Chain: {output}\n");

                output = JsonConvert.SerializeObject(certContext.SslPolicyErrors, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());
                Log.Logger.Information($"SslPolicyErrors: {output}\n");

                return true;
            };

            IMqttClientOptions mqttClientOptions = mqttClientOptionsBuilder
                .WithClientId(clientId)
                .WithTcpServer(mqttSettings.Mqtt.Host, mqttSettings.Mqtt.Port)
                .WithCredentials(mqttSettings.Mqtt.Username, mqttSettings.Mqtt.Password)
                .WithTls(mcobtp)
                .WithCleanSession(mqttSettings.Mqtt.CleanSession)
                .WithProtocolVersion(mqttSettings.Mqtt.ProtocolVersionAsEnum)
                .Build();

            mqttClient.UseApplicationMessageReceivedHandler(async e =>
            {
                Log.Logger.Debug("### RECEIVED APPLICATION MESSAGE ###");
                string topic = e.ApplicationMessage.Topic;
                Log.Logger.Debug($"+ Topic = {topic}");
                string payload;
                if (topic.ToLower() == "messages" || topic.ToLower().StartsWith("messages/indexation/") || topic.ToLower().StartsWith("transactions/"))
                {
                    //binary -> display as base64
                    payload = Convert.ToBase64String(e.ApplicationMessage.Payload);
                }
                else
                {
                    //json
                    payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload).BeautifyJson();

                    if (topic.ToLower() == "messages/referenced")
                    {
                        dynamic payloadObj = JsonConvert.DeserializeObject(payload);

                        string ledgerInclusionState = payloadObj["ledgerInclusionState"];

                        long countForLedgerInclusionState = 0;
                        countPerledgerInclusionState.TryGetValue(ledgerInclusionState, out countForLedgerInclusionState);
                        countPerledgerInclusionState[ledgerInclusionState] = countForLedgerInclusionState + 1;

                        string messageId = payloadObj["messageId"];
                        if (ledgerInclusionState.ToLower() == "included")
                        {
                            Message message = GetMessageDetails(messageId).Result;
                            Transaction transaction = new Transaction(messageId, message);

                            long amountIotas = transaction.Message.Data.Payload.Essence.Outputs.Sum(output => output.Amount);

                            string s = $"Included tx: {messageId} with amount {amountIotas.ConvertToBigestUnit2(out _, out _)}";

                            EssencePayload essencePayload = message.Data.Payload.Essence.Payload;
                            if (essencePayload != null)
                            {
                                string indexAsStr = essencePayload.Index.HexStringToString();
                                s = $"{s} and index {indexAsStr}";

                                long countForValueTxIndex = 0;
                                countPerValueTxIndex.TryGetValue(indexAsStr, out countForValueTxIndex);
                                countPerValueTxIndex[indexAsStr] = countForValueTxIndex + 1;

                                long sumAmountForValueTxIndex = 0;
                                totalAmountPerValueTxIndex.TryGetValue(indexAsStr, out sumAmountForValueTxIndex);
                                totalAmountPerValueTxIndex[indexAsStr] = sumAmountForValueTxIndex + amountIotas;

                                string essencePayloadData = essencePayload.Data.HexStringToString();
                                if (essencePayloadData != "")
                                {
                                    if ((essencePayloadData.Trim().StartsWith("{")) && (essencePayloadData.Trim().EndsWith("}")))
                                        essencePayloadData = essencePayloadData.BeautifyJson();
                                    s = $"{s} and data{Environment.NewLine}{essencePayloadData}";
                                }
                            }
                            Log.Logger.Information(s);

                            Log.Logger.Debug($"{TxTag}{Environment.NewLine}{transaction}");

                            //Todo: make a ED25519 to wallet address and vice versa
                            //address = iota1qzh2qpy03j4lgf9x85e37nhtwyzluhlgvzyyl04j9fdx5f3kcldr26p4je4
                            string monitorED25519Address = "aea0048f8cabf424a63d331f4eeb7105fe5fe860884fbeb22a5a6a2636c7da35";
                            long requiredAmount = 1_000_000;
                            if (transaction.Message.Data.Payload.Essence.Outputs.Any(output =>
                                output.Address.AddressAddress == monitorED25519Address &&
                                output.Amount == requiredAmount)
                            )
                            {
                                Log.Logger.Information($"Received the required amount of {requiredAmount} on the monitored ED25519 address {monitorED25519Address}!");
                            }
                        }

                        if (ledgerInclusionState.ToLower() == "conflicting")
                        {
                            Log.Logger.Information($"Conflicting tx: {messageId}");
                        }
                    }

                }
                Log.Logger.Debug($"+ Payload = {payload}");
                Log.Logger.Debug($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
                Log.Logger.Debug($"+ Retain = {e.ApplicationMessage.Retain}");

            });

            MqttClientConnectResult mqttClientConnectResult = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

            if (mqttClientConnectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                foreach (string topic in mqttSettings.Mqtt.Topics)
                {
                    try
                    {
                        MqttClientSubscribeOptions mqttClientSubscribeOptions = new MqttClientSubscribeOptionsBuilder()
                            .WithTopicFilter(topic, MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce)
                            .Build();

                        await mqttClient.SubscribeAsync(mqttClientSubscribeOptions);
                    }
                    catch (Exception e)
                    {
                        Log.Logger.Error($"Error while subscribing to {topic}: {e.Message} - {e.InnerException?.Message}");
                    }
                };
            }
            else Log.Logger.Error($"Error while connecting: {mqttClientConnectResult.ResultCode}");

            return mqttClientConnectResult;
        }

        private static async Task<Message> GetMessageDetails(string messageId)
        {
            Log.Logger.Information($"Loading message with Id {messageId}");

            Message message = null;

            var retryPolicy = Policy
              .Handle<Exception>()
              .WaitAndRetry(
                5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (e, timeSpan, retryCount, context) =>
                {
                    // Add logic to be executed before each retry, such as logging
                    Log.Logger.Error($"Retry #{retryCount} message with Id {messageId}: {e.Message}{e.InnerException.Message}");
                }
              );

            var policyResult = retryPolicy
                .ExecuteAndCapture(() =>
                {
                    return restClient.GetJsonAsync<Message>($"{settings.IotaNode.MessagesEndPointPath}{messageId}").Result;
                }
                );

            message = policyResult.Result;
            Log.Logger.Debug($"Result: {message}");
            return message;
        }

        private static RestClient InitRestClient()
        {
            var options = new RestClientOptions(settings.IotaNode.ApiUrl)
            {
                ThrowOnAnyError = true,
                Timeout = 1000
            };
            var client = new RestClient(options);
            client.UseNewtonsoftJson();

            return client;
        }

        private static void InitLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(settings.Logging.MinimumLevel)
                .WriteTo.Console(restrictedToMinimumLevel: settings.Logging.Console.RestrictedToMinimumLevel)
                .WriteTo.Logger(logconfig => logconfig
                    .Filter.ByExcluding(y => y.MessageTemplate.ToString().Contains(TxTag))
                    .WriteTo.File(
                        path: settings.Logging.File.Path,
                        rollingInterval: settings.Logging.File.RollingInterval,
                        rollOnFileSizeLimit: settings.Logging.File.RollOnFileSizeLimit,
                        restrictedToMinimumLevel: settings.Logging.File.RestrictedToMinimumLevel))
                .WriteTo.Logger(logconfig => logconfig
                    .Filter.ByIncludingOnly(y => y.MessageTemplate.ToString().Contains(TxTag))
                    .WriteTo.File(
                        path: settings.Logging.TxLog.Path,
                        rollingInterval: RollingInterval.Infinite))
                .CreateLogger();
        }

        public static Settings LoadSettings(string filename)
        {
            if (!System.IO.File.Exists(filename))
            {
                ShowSyntax();
                Environment.Exit(1);
            }

            string settingsJson = System.IO.File.ReadAllText(filename);

            Settings loadedSettings = null;
            try
            {
                loadedSettings = JsonConvert.DeserializeObject<Settings>(settingsJson);
            }
            catch (Exception e)
            {
                Log.Logger.Error($"{e.Message} - {e.InnerException?.Message}");
                Environment.Exit(1);
            }

            return loadedSettings;
        }

        private static string ParseArguments(string[] args)
        {
            if (args.Length > 1)
            {
                Log.Logger.Fatal("Error in arguments!");
                Environment.Exit(1);
            }

            string settingsFile;
            if (args.Length == 0)
            {
                string currentFolder = Directory.GetCurrentDirectory();
                settingsFile = "settings.json";
            }
            else
            {
                settingsFile = args[0];
            }

            return settingsFile;
        }

        private static void ShowSyntax()
        {
            Log.Logger.Information("Syntax: <program> [settingsfile]");
            Log.Logger.Information("\nSettingsfile: MqttSslProtocol values supported: None, SSLv3, TLSv1_0, TLSv1_1, TLSv1_2");
        }
    }
}
