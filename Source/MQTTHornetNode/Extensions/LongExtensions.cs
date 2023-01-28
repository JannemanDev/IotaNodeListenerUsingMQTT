using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTClient.Extensions
{
    public static class LongExtensions
    {
        public static string ConvertToBiggestUnit(this long amountIotas, out decimal amount, out string unit)
        {
            _ConvertToBiggestUnit(amountIotas, out amount, out unit);
            string s = $"{amount,6:0.00} {unit}";
            return s;
        }

        private static void _ConvertToBiggestUnit(decimal amountIotas, out decimal amount, out string unit)
        {
            decimal absAmountIotas = Math.Abs(amountIotas);

            if (absAmountIotas >= 1_000_000_000_000)
            {
                amount = amountIotas / 1_000_000_000_000;
                unit = "Ti";
            }
            else if (absAmountIotas >= 1_000_000_000)
            {
                amount = amountIotas / 1_000_000_000;
                unit = "Gi";
            }
            else if (absAmountIotas >= 1_000_000)
            {
                amount = amountIotas / 1_000_000;
                unit = "Mi";
            }
            else if (absAmountIotas >= 1_000)
            {
                amount = amountIotas / 1_000;
                unit = "Ki";
            }
            else
            {
                amount = amountIotas;
                unit = "i";
            }
        }
    }
}
