using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTClient.Extensions
{
    public static class StringExtensions
    {
        public static string BeautifyJson(this string str)
        {
            var obj = JsonConvert.DeserializeObject(str);
            string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            return json;
        }

        public static string HexStringToString(this string HexString)
        {
            string stringValue = "";
            for (int i = 0; i < HexString.Length / 2; i++)
            {
                string hexChar = HexString.Substring(i * 2, 2);
                int hexValue = Convert.ToInt32(hexChar, 16);
                stringValue += Char.ConvertFromUtf32(hexValue);
            }
            return stringValue;
        }
    }
}
