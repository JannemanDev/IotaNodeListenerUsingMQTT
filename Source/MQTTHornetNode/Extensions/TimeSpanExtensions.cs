using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTClient.Extensions
{
    public static class TimeSpanExtensions
    {
        public static string ConvertToFriendlyString(this TimeSpan timespan)
        {
            if (timespan < TimeSpan.FromMinutes(1))
            {
                return $"{timespan.TotalSeconds:00} seconds";
            }
            else if (timespan < TimeSpan.FromHours(1))
            {
                return $"{timespan.Minutes:00}m:{timespan.Seconds:00}s";
            }
            else if (timespan < TimeSpan.FromDays(1))
            {
                return $"{timespan.Hours:00}h:{timespan.Minutes:00}m:{timespan.Seconds:00}s";
            }
            else return $"{timespan.Days:00}d:{timespan.Hours:00}h:{timespan.Minutes:00}m:{timespan.Seconds:00}s";
        }
    }
}
