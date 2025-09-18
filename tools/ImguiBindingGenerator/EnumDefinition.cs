using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Common
{
    public enum EnumType
    {
        Enum,
        Bitmask,
    }

    public class EnumDefinition
    {
        public string Name;
        public string FrienlyName;
        public EnumType Type;
        public List<EnumValue> Values = new List<EnumValue>();

        internal static EnumDefinition FromJson(JProperty t)
        {
            EnumDefinition newEnum = new EnumDefinition();
            newEnum.Name = t.Name;
            newEnum.Type = t.Name.Contains("Flags") ? EnumType.Bitmask : EnumType.Enum;
            newEnum.FrienlyName = t.Name.EndsWith("_") ? t.Name.Substring(0, t.Name.Length - 1) : t.Name;

            foreach (var v in t.Values())
            {
                newEnum.Values.Add(EnumValue.FromJson(v, t.Name));
            }
            
            return newEnum;
        }
    }

    public class EnumValue
    {
        public string Name;
        public string FriendlyName;
        public int Value;

        internal static EnumValue FromJson(JToken v, string enumName)
        {
            EnumValue newValue = new EnumValue();
            newValue.Name = v["name"].ToString();

            string stringValue = v["calc_value"].ToString();
            newValue.Value = int.Parse(stringValue);

            if (newValue.Name.Contains(enumName))
            {
                int index = newValue.Name.IndexOf('_');
                newValue.FriendlyName = newValue.Name.Substring(index + 1);

                // Corner case in ImGuiKey enum
                if (int.TryParse(newValue.FriendlyName, out _))
                    newValue.FriendlyName = $"_{newValue.FriendlyName}";
            }
            else
            {
                newValue.FriendlyName = newValue.Name;
            }

            return newValue;
        }
    }
}