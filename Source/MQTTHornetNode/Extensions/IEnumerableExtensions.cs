using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTClient.Extensions
{
    public static class IEnumerableExtensions
    {
        public static int MaxOrDefault<T>(this IEnumerable<T> enumeration, Func<T, int> selector)
        {
            return enumeration.Any() ? enumeration.Max(selector) : default(int);
        }

        public static int SumOrDefault<T>(this IEnumerable<T> enumeration, Func<T, int> selector)
        {
            return enumeration.Any() ? enumeration.Sum(selector) : default(int);
        }

        public static long SumOrDefault<T>(this IEnumerable<T> enumeration, Func<T, long> selector)
        {
            return enumeration.Any() ? enumeration.Sum(selector) : default(long);
        }
    }
}
