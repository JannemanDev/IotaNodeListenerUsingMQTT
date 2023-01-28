using MQTTClient.Extensions;
using MQTTHornetNode.Models.Api.Messages;
using MQTTHornetNode.Models.Api.Outputs;
using MQTTHornetNode.Models.Api.Transactions;
using MQTTHornetNode.Models.WhatsMyIp;
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
    internal class WhatMyIpService
    {
        private readonly RestClient restClient;

        public WhatMyIpService(string apiUrl)
        {
            var options = new RestClientOptions(apiUrl)
            {
                ThrowOnAnyError = true,
                Timeout = 5000          //1 second is not enough!
            };
            restClient = new RestClient(options);
            restClient.UseNewtonsoftJson();
        }

        public IpResponse GetIp()
        {
            string s = $"Requesting Ip";
            Log.Logger.Information(s);

            IpResponse ipResponse = null;

            var retryPolicy = Policy
              .Handle<Exception>()
              .WaitAndRetry(
                5,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (e, timeSpan, retryCount, context) =>
                {
                    // Add logic to be executed before each retry, such as logging
                    Log.Logger.Error($"Retry #{retryCount} {s}: {e.Message}{e.InnerException.Message}");
                }
              );

            var policyResult = retryPolicy
                .ExecuteAndCapture(() =>
                {
                    return restClient.GetJsonAsync<IpResponse>("?format=json").Result;
                }
                );

            ipResponse = policyResult.Result;
            Log.Logger.Debug($"Response: {ipResponse.AsJson()}");

            return ipResponse;
        }
    }
}
