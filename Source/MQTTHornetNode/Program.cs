using MQTTClient.Configuration;
using MQTTClient.Extensions;
using MQTTHornetNode;
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
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MQTTClient
{

    public class Program
    {
        static Settings settings;

        static DateTime startedMonitoringAt;
        static Timer timer;

        static PushOverService pushOverService;
        static IotaNodeService iotaNodeService;
        static MqttClientListener mqttClientListener;

        public static string ApplicationPath;
        public static string RunningOnMachineSince;
        public static string Version;

        static async Task Main(string[] args)
        {
            ApplicationPath = AppContext.BaseDirectory;

            DefaultLogging();

            string settingsFile = ParseArguments(args);
            settings = LoadSettings(settingsFile);

            InitLogging();

            string filename;
            filename = Path.Combine(Program.ApplicationPath, "Logs", "TransactionsForMonitoredWallets.json");
            Log.Information($"Saving TransactionsForMonitoredWallets to: {filename}");

            var compileTime = new DateTime(Builtin.CompileTime, DateTimeKind.Utc).ToLocalTime();
            Version = $"Wallet Watcher v0.1 - BuildDate {compileTime}";

            WhatMyIpService whatMyIpService = new WhatMyIpService("https://api.ipify.org");
            string ip = whatMyIpService.GetIp().Ip;

            string localIp = GetLocalIPAddress();

            RunningOnMachineSince = $"{Version}\nRunning on {Environment.MachineName}\nSince {DateTime.Now}\nLocal ip {localIp}\nPublic ip {ip}";

            Log.Logger.Information("");
            Log.Logger.Information(Version);
            Log.Logger.Information($"Application path is {ApplicationPath}");
            Log.Logger.Information($"Loaded settings from: {settingsFile}");
            Log.Logger.Information($"Name of this instance is {settings.Application.Name}");
            Log.Logger.Information(RunningOnMachineSince);
            Log.Logger.Information("");

            if (!settings.Application.RunAsService)
            {
                Log.Logger.Information("Use [escape] to quit");
                Log.Logger.Information("Use [spacebar] to send a testmessage");
                Log.Logger.Information("Use [I] to print info about all received messages\n");
            }
            else
            {
                Log.Logger.Information("Running as a service. Press CTRL-C to quit...");
            }

            pushOverService = new PushOverService(settings.PushOver.EndPoint, settings.PushOver.ApiToken, settings.PushOver.GroupOrUserKeys);
            //await pushOverService.SendNotificationsAsync("test title", "test message");
            //return;

            iotaNodeService = new IotaNodeService(settings.IotaNode.ApiUrl, settings.IotaNode.MessagesEndPointPath, settings.IotaNode.UtxoEndPointPath, settings.IotaNode.TransactionsEndPointPath);
            //await iotaNodeService.GetMessageDetails("ddb045f6f4f1fa90818b8d0430edb7efc66cc58373191928aefc060420507533");
            //return;

            mqttClientListener = new MqttClientListener(settings.Application.Name, pushOverService, iotaNodeService, settings, settings.MonitorWalletAddresses);

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
                            mqttClientListener.MqttClient.DisconnectedHandler = null;
                            await mqttClientListener.MqttClient.DisconnectAsync();
                            Environment.Exit(0);
                        }

                        //Info
                        if (cki.Key == ConsoleKey.I) PrintInfo();

                        //Send
                        if (cki.Key == ConsoleKey.Spacebar && settings.Mqtt.Topics.Any())
                        {
                            string message = "{ \"msg\": \"test\" }";
                            string topic = settings.Mqtt.Topics[0]; //using the first topic as example
                            await mqttClientListener.MqttClient.PublishAsync(topic, message, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce, false);
                            Log.Logger.Information($"Publishing \"{message}\" to {settings.Mqtt.Uri} in topic \"{topic}\"");
                        }
                    }
                }
            }
        }

        private static void InitTimer()
        {
            int seconds = settings.Application.PrintTxsInfoEveryXMinutes;

            Log.Logger.Information($"Setting timer to {seconds} minute(s)");

            timer = new Timer((object state) => PrintInfo(), null, 0, (int)TimeSpan.FromMinutes(seconds).TotalMilliseconds);
        }

        private static void PrintInfo()
        {
            Log.Logger.Information($"Running time: {(DateTime.Now - startedMonitoringAt).ToString(@"dd\.hh\:mm\:ss")}");

            Log.Logger.Information("Count per ledger inclusion state:");

            mqttClientListener.CountPerledgerInclusionState.ToList()
                .OrderBy(messageCountPerType => messageCountPerType.Key) //sort on state
                .ToList()
                .ForEach(messageCountForType => Log.Logger.Information($" {messageCountForType.Key,13} ={messageCountForType.Value,7}x"));

            Log.Logger.Information("Count per index of a value transaction:");

            int longestString = mqttClientListener.CountPerValueTxIndex.ToList().MaxOrDefault(a => a.Key.Length);

            mqttClientListener.CountPerValueTxIndex.ToList()
                .OrderBy(countPerIndex => countPerIndex.Key) //sort on index
                .ToList()
                .ForEach(countForIndex =>
                {
                    string s = countForIndex.Key.ToString().PadLeft(longestString);
                    Log.Logger.Information($" {s} ={countForIndex.Value,4}x");
                });

            Log.Logger.Information("Total amount of value transferred per index:");

            longestString = mqttClientListener.TotalAmountPerValueTxIndex.ToList().MaxOrDefault(a => a.Key.Length);

            mqttClientListener.TotalAmountPerValueTxIndex.ToList()
                .OrderBy(totalAmountPerIndex => totalAmountPerIndex.Key) //sort on totalAmount
                .ToList()
                .ForEach(totalAmountForIndex =>
                {
                    string s = totalAmountForIndex.Key.ToString().PadLeft(longestString);
                    Log.Logger.Information($" {s} ={totalAmountForIndex.Value.ConvertToBiggestUnit(out _, out _),10}");
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

        private static void InitLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(settings.Logging.MinimumLevel)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: settings.Logging.Console.RestrictedToMinimumLevel)
                .WriteTo.Logger(logconfig => logconfig
                    .Filter.ByExcluding(y => y.MessageTemplate.ToString().Contains(Settings.TxTag))
                    .WriteTo.File(
                        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                        path: settings.Logging.File.Path,
                        rollingInterval: settings.Logging.File.RollingInterval,
                        rollOnFileSizeLimit: settings.Logging.File.RollOnFileSizeLimit,
                        restrictedToMinimumLevel: settings.Logging.File.RestrictedToMinimumLevel))
                .WriteTo.Logger(logconfig => logconfig
                    .Filter.ByIncludingOnly(y => y.MessageTemplate.ToString().Contains(Settings.TxTag))
                    .WriteTo.File(
                        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                        path: settings.Logging.TxLog.Path,
                        rollingInterval: RollingInterval.Infinite))
                .WriteTo.Logger(logconfig => logconfig
                    .Filter.ByIncludingOnly(y => y.MessageTemplate.ToString().Contains(Settings.MonitorTag))
                    .WriteTo.File(
                        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                        path: settings.Logging.MonitorLog.Path,
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
            Log.Logger.Information("\nSettingsfile: MqttSslProtocol values supported: None, SSL2, SSL3, TLS10, TLS11, TLS12, TLS13");
        }

        public static string GetLocalIPAddress()
        {
            string localIP;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }
            return localIP;
        }
    }
}
