using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Common
{
    public class Specification
    {
        public List<EnumDefinition> Enums = new List<EnumDefinition>();
        public List<StructDefinition> Structs = new List<StructDefinition>();
        public List<FunctionsDefinition> Funtions = new List<FunctionsDefinition>();

        public static Specification FromFiles(string structs_and_enums_json, string definitions_json)
        {
            JObject typesJson;
            using (StreamReader fs = File.OpenText(structs_and_enums_json))
            using (JsonTextReader jr = new JsonTextReader(fs))
            {
                typesJson = JObject.Load(jr);
            }

            JObject functionsJson;
            using (StreamReader fs = File.OpenText(definitions_json))
            using (JsonTextReader jr = new JsonTextReader(fs))
            {
                functionsJson = JObject.Load(jr);
            }

            Specification spec = new Specification();

            var locations = typesJson["locations"];
            var customDefinedTypes = new[] { "ImGuiStoragePair" };

            // Enums
            spec.Enums = typesJson["enums"].Select(t =>
            {
                if (locations?[((JProperty)t).Name]?.Value<string>().Contains("internal") ?? false)
                    return null;

                return EnumDefinition.FromJson((JProperty)t);
            }).Where(x => x != null).ToList();

            // Structs
            spec.Structs = typesJson["structs"].Select(s =>
            {
                if (locations?[((JProperty)s).Name]?.Value<string>().Contains("internal") ?? false)
                    return null;

                if (customDefinedTypes.Contains(((JProperty)s).Name))
                    return null;

                return StructDefinition.FromJson((JProperty)s);
            }).Where(x => x != null).ToList();

            // Functions
            spec.Funtions = functionsJson.Children().Select(f =>
            {
                return FunctionsDefinition.FromJson(f, spec);
            }).Where(x => x != null).ToList();

            return spec;
        }
    }
}
