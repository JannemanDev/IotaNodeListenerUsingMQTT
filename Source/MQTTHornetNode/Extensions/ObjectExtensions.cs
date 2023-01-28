using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTClient.Extensions
{
    public static class ObjectExtensions
    {
        public static string AsJson(this object obj, Formatting formatting = Formatting.Indented)
        {
            string json = JsonConvert.SerializeObject(obj, formatting);
            return json;
        }
    }
}
