using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTHornetNode.Models
{
    public class Transaction
    {
        public string Id { get; set; }
        public Message Message { get; set; }

        public Transaction(string id, Message message)
        {
            Id = id;
            Message = message;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    public class Message
    {
        [JsonProperty("data")]
        private Data _data = null; //to support JSON message structure of Hornet node v1.2.1+

        [JsonProperty("message")]
        private Data _message = null; //to support JSON message structure from https://explorer-api.iota.org/search/mainnet/

        [JsonIgnore]
        public Data Data { get { 
                return _data == null ? _message : _data;
            } 
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    public partial class Data
    {
        [JsonProperty("networkId")]
        public string NetworkId { get; set; }

        [JsonProperty("parentMessageIds")]
        public string[] ParentMessageIds { get; set; }

        [JsonProperty("payload")]
        public DataPayload Payload { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }
    }

    public partial class DataPayload
    {
        [JsonProperty("type")]
        public long Type { get; set; }

        [JsonProperty("essence")]
        public Essence Essence { get; set; }

        [JsonProperty("unlockBlocks")]
        public UnlockBlock[] UnlockBlocks { get; set; }
    }

    public partial class Essence
    {
        [JsonProperty("type")]
        public long Type { get; set; }

        [JsonProperty("inputs")]
        public Input[] Inputs { get; set; }

        [JsonProperty("outputs")]
        public Output[] Outputs { get; set; }

        [JsonProperty("payload")]
        public EssencePayload Payload { get; set; }
    }

    public partial class Input
    {
        [JsonProperty("type")]
        public long Type { get; set; }

        [JsonProperty("transactionId")]
        public string TransactionId { get; set; }

        [JsonProperty("transactionOutputIndex")]
        public long TransactionOutputIndex { get; set; }
    }

    public partial class Output
    {
        [JsonProperty("type")]
        public long Type { get; set; }

        [JsonProperty("address")]
        public Address Address { get; set; }

        [JsonProperty("amount")]
        public long Amount { get; set; }
    }

    public partial class Address
    {
        [JsonProperty("type")]
        public long Type { get; set; }

        [JsonProperty("address")]
        public string AddressAddress { get; set; }
    }

    public partial class EssencePayload
    {
        [JsonProperty("type")]
        public long Type { get; set; }

        [JsonProperty("index")]
        public string Index { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }
    }

    public partial class UnlockBlock
    {
        [JsonProperty("type")]
        public long Type { get; set; }

        [JsonProperty("signature")]
        public SignatureDetails Signature { get; set; }
    }

    public partial class SignatureDetails
    {
        [JsonProperty("type")]
        public long Type { get; set; }

        [JsonProperty("publicKey")]
        public string PublicKey { get; set; }

        [JsonProperty("signature")]
        public string Signature { get; set; }
    }
}
