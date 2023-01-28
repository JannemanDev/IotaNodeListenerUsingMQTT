using MQTTnet.Formatter;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace MQTTClient.Configuration
{
    public class Settings
    {
        public static readonly string TxTag = "[Tx]";
        public static readonly string MonitorTag = "[Monitor]";

        public App Application { get; set; }
        public IotaNode IotaNode { get; set; }
        public MonitorWalletAddress[] MonitorWalletAddresses { get; set; }
        public Mqtt Mqtt { get; set; }
        public PushOver PushOver { get; set; }
        public Logging Logging { get; set; }
    }

    public enum WalletChangeType
    {
        Withdrawal,
        Deposit,
        Both
    }

    public class MonitorWalletAddress
    {
        public bool Enabled { get; set; }
        public WalletChangeType WalletChangeType { get; set; }
        public string WalletAddress { get; set; }
        public string Description { get; set; }
        public long? AmountEqual { get; set; }
        public long? AmountMinimum { get; set; }
        public long? AmountMaximum { get; set; }

        public List<string> IncludeGroupOrUserKeys { get; set; } = new List<string>(); //optional
        public List<string> ExcludeGroupOrUserKeys { get; set; } = new List<string>(); //optional

        public MonitorWalletAddress(bool enabled, WalletChangeType walletChangeType, string walletAddress, string description) : this(enabled, walletChangeType, walletAddress, description, null, null, null, new List<string>(), new List<string>())
        {
        }

        [JsonConstructor]
        public MonitorWalletAddress(bool enabled, WalletChangeType walletChangeType, string walletAddress, string description, long? amountEqual,
                                    long? amountMinimum, long? amountMaximum, List<string> includeGroupOrUserKeys, List<string> excludeGroupOrUserKeys)
        {
            Enabled = enabled;
            WalletAddress = walletAddress;
            Description = description;
            AmountEqual = amountEqual;
            AmountMinimum = amountMinimum;
            AmountMaximum = amountMaximum;
            IncludeGroupOrUserKeys = includeGroupOrUserKeys.Where(k => k != "").Distinct().ToList();
            ExcludeGroupOrUserKeys = excludeGroupOrUserKeys.Where(k => k != "").Distinct().ToList();

            if (AmountEqual != null && (AmountMinimum != null || AmountMaximum != null))
                throw new ArgumentException($"Monitoring wallet address {WalletAddress}: you can not set AmountEqual *and* also set any of AmountMinimum or AmountMaximum!");
        }

        public bool AmountSatifiesConditions(long amount)
        {
            if (AmountEqual == null && AmountMinimum == null && AmountMaximum == null) return true;

            if (AmountEqual != null) return AmountEqual == amount;
            else
            {
                bool result = true;
                if (AmountMinimum != null) result = result && (amount >= AmountMinimum);
                if (AmountMaximum != null) result = result && (amount <= AmountMaximum);

                return result;
            }
        }
    }

    public class App
    {
        public string Name { get; set; }
        public bool RunAsService { get; set; }
        public int PrintTxsInfoEveryXMinutes { get; set; }
    }

    public class IotaNode
    {
        public string ApiUrl { get; set; }
        public string MessagesEndPointPath { get; set; }
        public string UtxoEndPointPath { get; set; }
        public string TransactionsEndPointPath { get; set; }
    }

    public class Mqtt
    {
        public string Uri { get; set; }
        public string SslProtocol { get; set; }

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
        public string ProtocolVersion { get; set; }
        public bool UseWebSocket4Net { get; set; }
        public bool IgnoreCertificateChainErrors { get; set; }
        public bool IgnoreCertificateRevocationErrors { get; set; }
        public bool AllowUntrustedCertificates { get; set; }
        public string[] Topics { get; set; }
        public bool CleanSession { get; set; }


        [JsonProperty("WebSocketParameters")]
        public WebSocketParameters WebSocketParameters { get; set; }

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
        public MonitorLog MonitorLog { get; set; }
        public TxLog TxLog { get; set; }
        public LogEventLevel MinimumLevel { get; set; }
        public File File { get; set; }
        public Console Console { get; set; }
    }

    public class MonitorLog
    {
        public string Path { get; set; }
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

    public class PushOver
    {
        public bool Enabled { get; set; }

        public string EndPoint { get; set; }
        public string ApiToken { get; set; }

        public List<string> GroupOrUserKeys { get; set; }

        [JsonConstructor]
        public PushOver(bool enabled, string endPoint, string apiToken, List<string> groupOrUserKeys)
        {
            Enabled = enabled;
            EndPoint = endPoint;
            ApiToken = apiToken;
            GroupOrUserKeys = groupOrUserKeys.Where(k => k != "").Distinct().ToList();
        }
    }

    public class Device
    {
        public string Name { get; set; }

        public bool Include { get; set; }

        public override string ToString()
        {
            return $"Device name {Name}";
        }
    }

    public class WebSocketParameters
    {
        [JsonProperty("RequestHeaders")]
        public Dictionary<string, string> RequestHeaders { get; set; }

        [JsonProperty("CookieCollection")]
        public CookieCollection CookieCollection { get; set; }
    }

    public class Cookie
    {
        [JsonProperty("Domain")]
        public string Domain { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Path")]
        public string Path { get; set; }

        [JsonProperty("Value")]
        public string Value { get; set; }
    }

}
