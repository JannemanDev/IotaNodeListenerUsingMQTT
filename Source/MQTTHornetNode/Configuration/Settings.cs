using MQTTnet.Formatter;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace MQTTClient.Configuration
{
    public class Settings
    {
        public App Application { get; set; }
        public IotaNode IotaNode { get; set; }
        public Mqtt Mqtt { get; set; }
        public Logging Logging { get; set; }
    }

    public class App
    {
        public bool RunAsService { get; set; }
        public int PrintTxsInfoEveryXMinutes { get; set; }
    }

    public class IotaNode
    {
        public string ApiUrl { get; set; }
        public string MessagesEndPointPath { get; set; }
    }

    public class Mqtt
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string SslProtocol { get; set; }
        public bool UseTls { get; set; }

        [JsonProperty(Required = Required.AllowNull)]
        public string ClientId { get; set; }

        [JsonIgnore]
        public SslProtocols SslProtocolAsEnum
        {
            get
            {
                switch (SslProtocol)
                {
                    case "None": return SslProtocols.None;
                    case "SSL2": return SslProtocols.Ssl2;
                    case "SSL3": return SslProtocols.Ssl3;
                    case "TLS10": return SslProtocols.Tls;
                    case "TLS11": return SslProtocols.Tls11;
                    case "TLS12": return SslProtocols.Tls12;
                    case "TLS13": return SslProtocols.Tls13;
                    default: throw new ArgumentException("SslProtocol values supported: None, SSL2, SSL3, TLS10, TLS11, TLS12, TLS13");
                }
            }
        }

        public string Username { get; set; }
        public string Password { get; set; }
        public string[] Topics { get; set; }
        public bool CleanSession { get; set; }
        public string ProtocolVersion { get; set; }

        [JsonIgnore]
        public MqttProtocolVersion ProtocolVersionAsEnum
        {
            get
            {
                switch (ProtocolVersion)
                {
                    case "V310": return MqttProtocolVersion.V310;
                    case "V311": return MqttProtocolVersion.V311;
                    case "V500": return MqttProtocolVersion.V500;
                    default: throw new ArgumentException("ProtocolVersion values supported: V310, V311, V500");
                }
            }
        }
    }

    public class Logging
    {
        public TxLog TxLog { get; set; }
        public LogEventLevel MinimumLevel { get; set; }
        public File File { get; set; }
        public Console Console { get; set; }
    }

    public class TxLog
    {
        public string Path { get; set; }
    }

    public class File
    {
        public bool Enabled { get; set; }
        public string Path { get; set; }
        public RollingInterval RollingInterval { get; set; }
        public bool RollOnFileSizeLimit { get; set; }
        public LogEventLevel RestrictedToMinimumLevel { get; set; }
    }

    public class Console
    {
        public bool Enabled { get; set; }
        public LogEventLevel RestrictedToMinimumLevel { get; set; }
    }

}
