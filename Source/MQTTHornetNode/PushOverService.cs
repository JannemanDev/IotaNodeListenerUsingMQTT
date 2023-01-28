using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;

namespace MQTTHornetNode
{
    internal class PushOverService
    {
        private readonly RestClient restClient;
        private readonly string apiToken;
        private readonly List<string> groupOrUserKeys;

        public bool Enabled { get; set; }

        public PushOverService(string endPoint, string apiToken, List<string> groupOrUserKeys, RestClientOptions restClientOptions = null)
        {
            Enabled = true;
            this.apiToken = apiToken;
            this.groupOrUserKeys = groupOrUserKeys;

            if (restClientOptions == null)
                restClientOptions = new RestClientOptions($"{endPoint}")
                {
                    ThrowOnAnyError = true,
                    Timeout = 3000,
                };

            restClient = new RestClient(restClientOptions);
        }

        public async Task<bool> SendNotificationsAsync(string title, string message, List<string> groupOrUserKeys)
        {
            if (!Enabled) return true;

            bool sendAllSucceeded = true;

            foreach (var groupOrUserKey in groupOrUserKeys)
            {
                Log.Logger.Information($"Sending a notification {groupOrUserKey}:\n{title}\n{message}");


                var request = new RestRequest()
                {
                    Method = Method.Post
                };

                request.AddParameter("html", 1);
                request.AddParameter("token", apiToken);
                request.AddParameter("user", groupOrUserKey);
                request.AddParameter("title", title);
                request.AddParameter("message", message);

                request.AddHeader("cache-control", "no-cache");
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");

                RestResponse? response = null;
                try
                {
                    response = await restClient.ExecuteAsync(request);
                }
                catch (Exception e)
                {
                    Log.Logger.Error($"Error sending notification to {groupOrUserKey}:\n{e.Message}{Environment.NewLine}{e.InnerException.Message}");
                }
                finally
                {
                    if (response != null && !response.IsSuccessful)
                    {
                        Log.Logger.Error($"Error sending notification to {groupOrUserKey}:\n{response.ErrorMessage}{Environment.NewLine}{response.ErrorException.InnerException.Message}");
                        sendAllSucceeded = false;
                    }
                }
            }

            return sendAllSucceeded;
        }
    }
}
