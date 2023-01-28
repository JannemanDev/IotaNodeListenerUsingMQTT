using MQTTClient.Extensions;
using MQTTHornetNode.Models.MqttEvents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTHornetNode
{
    public class MonitoredWalletEventsForTransaction
    {
        private bool processed = false;

        public DateTime CreatedAt { get; set; }
        public DateTime ProcessedAt { get; set; }
        public bool Processed
        {
            get => processed;
            set //can only be processed once, and not be unprocessed
            {
                if (!processed && value)
                {
                    processed = true;
                    ProcessedAt = DateTime.Now;
                }
                if (processed && !value) throw new Exception($"MonitoredWalletEventsForTransaction already processed, can not be unprocessed:\n{this.AsJson()}");
            }
        }
        public List<MonitoredWalletEvent> MonitoredWalletEvents { get; set; }
        public string TxId { get; set; }

        //Todo: welke walletaddress behoren deze event? Zijn alle Bech32Address uit MonitoredWalletEvents altijd hetzelfde?

        public MonitoredWalletEventsForTransaction(string txId, MonitoredWalletEvent monitoredWalletEvent)
        {
            CreatedAt = DateTime.Now;
            TxId = txId;
            MonitoredWalletEvents = new List<MonitoredWalletEvent>();
        }
    }

    public class MonitoredWalletEvent
    {
        public string Bech32Address { get; set; }
        public string TxId { get; set; } //Is same as MonitoredWalletEventsForTransaction.TxId when IsSpent=false else it's txId of Spent transaction
        public int OutputIndex { get; set; }
        public string UtxoId => $"{TxId}{OutputIndex.ToString("D2")}00";
        public bool IsSpent { get; set; }
        public long Amount { get; set; }

        public MonitoredWalletEvent(string bech32Address, string txId, int outputIndex, long amount, bool isSpent)
        {
            Bech32Address = bech32Address;
            TxId = txId;
            OutputIndex = outputIndex;
            Amount = amount;
            IsSpent = isSpent;
        }
    }
}
