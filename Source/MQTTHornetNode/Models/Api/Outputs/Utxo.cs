using Bech32Lib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTHornetNode.Models.Api.Outputs
{
    // Api endpoint: /api/v1/outputs/{txId}{outputIndex}

    /* Description:
     * Pay attention: /api/v1/outputs/{outputId} returns different responses !
     * 
     * When isSpent=false:
            {
                "data": {
                    "messageId": "e02ea59d83c1342363ce1b5557e295845ef908d2c4f64368640140c105be0201",
                    "transactionId": "602a1552ff5febf1a5254844e1b4ae614b4b5e6c425be28296d68973187bc35d",
                    "outputIndex": 0,
                    "isSpent": false,
                    "ledgerIndex": 5208520,
                    "output": {
                        "type": 0,
                        "address": {
                            "type": 0,
                            "address": "0939578a5786472170bbf4ddd6e79966ccc8842a3294f24147822d9726c6b909"
                        },
                        "amount": 6500000
                    }
                }
            }

     * When isSpent=true you will get TWO extra properties:
     * 
	        {
            "data": {
                "messageId": "e02ea59d83c1342363ce1b5557e295845ef908d2c4f64368640140c105be0201",
                "transactionId": "602a1552ff5febf1a5254844e1b4ae614b4b5e6c425be28296d68973187bc35d",
                "outputIndex": 0,
                "isSpent": true,
            X   "milestoneIndexSpent": 5208526,
            X   "transactionIdSpent": "a036e1516920731afec3c24355a74536b3fe926080688c081b519cb8c0d62224",
                "ledgerIndex": 5208529,
                "output": {
                    "type": 0,
                    "address": {
                        "type": 0,
                        "address": "0939578a5786472170bbf4ddd6e79966ccc8842a3294f24147822d9726c6b909"
                    },
                    "amount": 6500000
                }
            }
     *
     */

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
        [JsonProperty("messageId")]
        public string MessageId { get; set; }

        [JsonProperty("transactionId")]
        public string TransactionId { get; set; }

        [JsonProperty("outputIndex")]
        public int OutputIndex { get; set; }

        [JsonProperty("isSpent")]
        public bool IsSpent { get; set; }

        [JsonProperty("milestoneIndexSpent")]
        public int MilestoneIndexSpent { get; set; } //OPTIONAL, can be empty!

        [JsonProperty("transactionIdSpent")]
        public string TransactionIdSpent { get; set; } //OPTIONAL, can be empty!

        [JsonProperty("ledgerIndex")]
        public int LedgerIndex { get; set; }

        [JsonProperty("output")]
        public Output Output { get; set; }
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

    public class Utxo
    {
        [JsonProperty("data")]
        public Data Data { get; set; }
    }
}
