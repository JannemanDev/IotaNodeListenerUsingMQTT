using Bech32Lib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTHornetNode.Models.Api.Transactions
{
    public class Address
    {
        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("address")]
        public string AddressAddress { get; set; }

        public string Bech32Address => Bech32.Encode("iota", AddressAddress);
    }

    public class Data
    {
        [JsonProperty("networkId")]
        public string NetworkId { get; set; }

        [JsonProperty("parentMessageIds")]
        public List<string> ParentMessageIds { get; set; }

        [JsonProperty("payload")]
        public Payload Payload { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }
    }

    public class Essence
    {
        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("inputs")]
        public List<Input> Inputs { get; set; }

        [JsonProperty("outputs")]
        public List<Output> Outputs { get; set; }

        [JsonProperty("payload")]
        public Payload Payload { get; set; }
    }

    public class Input
    {
        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("transactionId")]
        public string TransactionId { get; set; }

        [JsonProperty("transactionOutputIndex")]
        public int TransactionOutputIndex { get; set; }
        
        public string UtxoId => $"{TransactionId}{TransactionOutputIndex.ToString("D2")}00";
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

    public class Payload
    {
        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("essence")]
        public Essence Essence { get; set; }

        [JsonProperty("unlockBlocks")]
        public List<UnlockBlock> UnlockBlocks { get; set; }

        [JsonProperty("index")]
        public string Index { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }
    }

    public class Transaction
    {
        [JsonProperty("data")]
        public Data Data { get; set; }
    }

    public class Signature
    {
        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("publicKey")]
        public string PublicKey { get; set; }

        [JsonProperty("signature")]
        public string SignatureSignature { get; set; }
    }

    public class UnlockBlock
    {
        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("signature")]
        public Signature Signature { get; set; }

        [JsonProperty("reference")]
        public int? Reference { get; set; }
    }


}
