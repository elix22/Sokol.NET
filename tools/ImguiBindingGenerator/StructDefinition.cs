using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Common
{
    public class StructDefinition
    {
        public string Name;
        public List<Member> Members = new List<Member>();

        public static StructDefinition FromJson(JProperty elem)
        {
            StructDefinition s = new StructDefinition();
            s.Name = elem.Name;

            foreach (var m in elem.Values())
            {
                s.Members.Add(Member.FromJson(m));
            }

            return s;
        }
    }

    public class Member
    {
        public string Name;
        public string Type;
        public string TemplateType;
        public string Value;
        public string Size;
        public int Count;

        public static Member FromJson(JToken elem)
        {
            Member m = new Member();


            string label = m.Name = elem["name"].ToString();
            int startBracket = label.IndexOf('[');

            if (startBracket != -1)
            {
                m.Name = m.Name.Substring(0, startBracket);
            }

            if (label.EndsWith("Fn"))
            {
                m.Type = "IntPtr";
            }
            else
            {
                m.Type = elem["type"].ToString();
            }

            m.TemplateType = elem["template_type"]?.ToString();
            m.Size = elem["size"]?.ToString();
            m.Count = string.IsNullOrEmpty(m.Size) ? 0 : int.Parse(m.Size);

            return m;
        }
    }
}