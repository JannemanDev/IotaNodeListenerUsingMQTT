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

        public static string HexStringToString(this string hexString)
        {
            string stringValue = "";
            for (int i = 0; i < hexString.Length / 2; i++)
            {
                string hexChar = hexString.Substring(i * 2, 2);
                int hexValue = Convert.ToInt32(hexChar, 16);
                stringValue += Char.ConvertFromUtf32(hexValue);
            }
            return stringValue;
        }

        public static string TrimStart(this string str, string trimString)
        {
            return str.TrimStart(trimString, false);
        }

        public static string TrimStart(this string str, string trimString, bool ignoreCase)
        {
            if (str.StartsWith(trimString, ignoreCase, null)) return str.Remove(0, trimString.Length);
            else return str;
        }
        public static string TrimEnd(this string str, string trimString)
        {
            return str.TrimEnd(trimString, false);
        }

        public static string TrimEnd(this string str, string trimString, bool ignoreCase)
        {
            if (str.EndsWith(trimString, ignoreCase, null)) return str.Remove(str.Length - trimString.Length, trimString.Length);
            else return str;
        }
    }
}
