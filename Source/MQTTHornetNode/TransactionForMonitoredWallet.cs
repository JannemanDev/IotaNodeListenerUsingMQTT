using MQTTClient.Extensions;
using MQTTHornetNode.Models.Api.Messages;
using MQTTHornetNode.Models.Api.Outputs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTHornetNode
{
    public class TransactionForMonitoredWallet
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
                if (processed && !value) throw new Exception($"TransactionForMonitoredWallet already processed, can not be unprocessed:\n{this.AsJson()}");
            }
        }
        public string TxId { get; set; }

        public List<TxPart> TxParts { get; set; } = new List<TxPart>();

        //Todo: calculate value of tx

        public TransactionForMonitoredWallet(string txId)
        {
            CreatedAt = DateTime.Now;
            TxId = txId;
        }

        public void AddTxPart(TxPart txPart)
        {
            TxParts.Add(txPart);
        }

        public void AddMonitoredWalletEvent(MonitoredWalletEvent mwe)
        {
            TxPart txPart = new TxPart() { Amount = mwe.Amount, Bech32Address = mwe.Bech32Address, IsInput = mwe.IsSpent };
            TxParts.Add(txPart);
        }

        public void AddUtxo(Utxo utxo)
        {
            TxPart txPart = new TxPart() { Amount = utxo.Data.Output.Amount, Bech32Address = utxo.Data.Output.Address.Bech32Address, IsInput = utxo.Data.IsSpent };
            TxParts.Add(txPart);
        }
    }

    public class TxPart
    {
        public string Bech32Address { get; set; }
        public bool IsInput { get; set; }
        public bool IsOutput => !IsInput;
        public long Amount { get; set; }
    }
}
