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
        public static string ConvertToBigestUnit2(this long amountIotas, out decimal amount, out string unit)
        {
            ConvertToBigestUnit(amountIotas, out amount, out unit);
            string s = $"{amount,6:0.00} {unit}";
            return s;
        }

        private static void ConvertToBigestUnit(decimal amountIotas, out decimal amount, out string unit)
        {
            if (amountIotas >= 1_000_000_000_000)
            {
                amount = amountIotas / 1_000_000_000_000;
                unit = "Ti";
            }
            else if (amountIotas >= 1_000_000_000)
            {
                amount = amountIotas / 1_000_000_000;
                unit = "Gi";
            }
            else if (amountIotas >= 1_000_000)
            {
                amount = amountIotas / 1_000_000;
                unit = "Mi";
            }
            else if (amountIotas >= 1_000)
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
