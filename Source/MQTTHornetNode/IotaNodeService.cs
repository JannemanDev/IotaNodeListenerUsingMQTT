using MQTTClient.Extensions;
using MQTTHornetNode.Models.Api.Messages;
using MQTTHornetNode.Models.Api.Outputs;
using MQTTHornetNode.Models.Api.Transactions;
using Polly;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTHornetNode
{
    internal class IotaNodeService
    {
        private readonly RestClient restClient;
        private readonly string messagesEndPointPath;
        private readonly string utxoEndPointPath;
        private readonly string transactionEndPointPath;

        public IotaNodeService(string apiUrl, string messagesEndPointPath, string utxoEndPointPath, string transactionEndPointPath)
        {
            this.messagesEndPointPath = messagesEndPointPath;
            this.utxoEndPointPath = utxoEndPointPath;
            this.transactionEndPointPath = transactionEndPointPath;

            var options = new RestClientOptions(apiUrl)
            {
                ThrowOnAnyError = true,
                Timeout = 1000
            };
            restClient = new RestClient(options);
            restClient.UseNewtonsoftJson();
        }

        public Message GetMessageDetails(string messageId)
        {
            Log.Logger.Information($"Requesting message with Id {messageId}");

            Message message = null;

            var retryPolicy = Policy
              .Handle<Exception>()
              .WaitAndRetry(
                5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (e, timeSpan, retryCount, context) =>
                {
                    // Add logic to be executed before each retry, such as logging
                    Log.Logger.Error($"Retry #{retryCount} requesting message with Id {messageId}: {e.Message}{e.InnerException.Message}");
                }
              );

            var policyResult = retryPolicy
                .ExecuteAndCapture(() =>
                {
                    return restClient.GetJsonAsync<Message>($"{messagesEndPointPath}{messageId}").Result;
                }
                );

            message = policyResult.Result;
            Log.Logger.Debug($"Response: {message.AsJson()}");

            return message;
        }

        public Utxo GetUtxoDetails(string outputId)
        {
            Log.Logger.Information($"Requesting utxo details for outputId {outputId}");

            Utxo utxo = null;

            var retryPolicy = Policy
              .Handle<Exception>()
              .WaitAndRetry(
                5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (e, timeSpan, retryCount, context) =>
                {
                    // Add logic to be executed before each retry, such as logging
                    Log.Logger.Error($"Retry #{retryCount} requesting utxo details for outputId {outputId}: {e.Message}{e.InnerException.Message}");
                }
              );

            var policyResult = retryPolicy
                .ExecuteAndCapture(() =>
                {
                    return restClient.GetJsonAsync<Utxo>($"{utxoEndPointPath}{outputId}").Result;
                }
                );

            utxo = policyResult.Result;
            Log.Logger.Debug($"Response: {utxo.AsJson()}");

            return utxo;
        }

        public Transaction GetTransactionDetails(string transactionId)
        {
            Log.Logger.Information($"Requesting transaction details for transaction id {transactionId}");

            Transaction transaction = null;

            var retryPolicy = Policy
              .Handle<Exception>()
              .WaitAndRetry(
                5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (e, timeSpan, retryCount, context) =>
                {
                    // Add logic to be executed before each retry, such as logging
                    Log.Logger.Error($"Retry #{retryCount} requesting transaction details for transaction id {transactionId}: {e.Message}{e.InnerException.Message}");
                }
              );

            var policyResult = retryPolicy
                .ExecuteAndCapture(() =>
                {
                    //Todo: when new hornet node version is released use /included-message/metadata endpoint which includes messageId
                    // which is needed to generate The Tangle Explorer link
                    return restClient.GetJsonAsync<Transaction>($"{transactionEndPointPath}{transactionId}/included-message").Result;
                }
                );

            transaction = policyResult.Result;
            Log.Logger.Debug($"Response: {transaction.AsJson()}");

            return transaction;
        }
    }
}
