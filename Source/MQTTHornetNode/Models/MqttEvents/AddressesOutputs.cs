using Bech32Lib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTHornetNode.Models.MqttEvents
{
    public class Address
    {
        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("address")]
        public string AddressAddress { get; set; }

        public string Bech32Address => Bech32.Encode("iota", AddressAddress);
    }

    public class Output
    {
        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("address")]
        public Address Address { get; set; }

        [JsonProperty("amount")]
        public long Amount { get; set; }
    }

    public class AddressesOutputs
    {
        [JsonProperty("messageId")]
        public string MessageId { get; set; }

        [JsonProperty("transactionId")]
        public string TransactionId { get; set; }

        [JsonProperty("outputIndex")]
        public int OutputIndex { get; set; }

        public string UtxoId => $"{TransactionId}{OutputIndex.ToString("D2")}00";

        [JsonProperty("isSpent")]
        public bool IsSpent { get; set; }

        [JsonProperty("ledgerIndex")]
        public int LedgerIndex { get; set; }

        [JsonProperty("output")]
        public Output Output { get; set; }
    }
}
