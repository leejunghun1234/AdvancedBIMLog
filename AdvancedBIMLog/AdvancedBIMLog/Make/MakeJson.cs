using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedBIMLog.Make
{
    internal class MakeJson
    {
        public static void MakeJsonFile(string filePath, JObject jsonfile)
        {
            File.WriteAllText(filePath, JsonConvert.SerializeObject(jsonfile, Formatting.Indented), System.Text.Encoding.UTF8);
        }

        public static void MakeJsonFile(string filePath, JArray jsonfile)
        {
            File.WriteAllText(filePath, JsonConvert.SerializeObject(jsonfile, Formatting.Indented), System.Text.Encoding.UTF8);
        }
    }
}
