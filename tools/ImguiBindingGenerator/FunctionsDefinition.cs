using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    public class FunctionsDefinition
    {
        public List<OverloadDefinition> Overloads = new List<OverloadDefinition>();

        public static FunctionsDefinition FromJson(JToken f, Specification spec)
        {
            FunctionsDefinition func = new FunctionsDefinition();

            foreach (var o in f.Values())
            {
                // Skip internal functions
                string location = o["location"]?.ToString();
                string funcname = o["funcname"]?.ToString();
                if (!string.IsNullOrEmpty(location) && location.Contains("internal")
                    || string.IsNullOrEmpty(funcname))
                    continue;

                string typename = o["stname"]?.ToString();
                if (!string.IsNullOrEmpty(typename))
                {
                    if (!spec.Structs.Any(x => x.Name == typename))
                        return null;
                }

                func.Overloads.Add(OverloadDefinition.FromJson(o));
            }

            return func.Overloads.Count == 0 ? null : func;
        }
    }

    public class OverloadDefinition
    {
        public string FuncName;
        public string ReturnType;
        public string Signature;
        public bool IsConstructor;
        public bool IsDestructor;
        public List<Param> Params = new List<Param>();

        public static OverloadDefinition FromJson(JToken f)
        {
            OverloadDefinition overload = new OverloadDefinition();
            overload.FuncName = f["ov_cimguiname"].ToString();
            overload.ReturnType = f["ret"]?.ToString() ?? "void";
            overload.Signature = f["signature"]?.ToString();
            overload.IsConstructor = f.Value<bool>("constructor");
            overload.IsDestructor = f.Value<bool>("destructor");

            foreach (var a in f["argsT"])
            {
                overload.Params.Add(Param.FromJson(a));
            }

            foreach (var d in f["defaults"])
            {
                string pName = ((JProperty)d).Name.ToString();
                var param = overload.Params.Find(p => p.Name == pName);

                if(param != null)
                    param.DefaultValue = ((JProperty)d).Value.ToString();
            }

            return overload;
        }

        [Flags]
        public enum ParametersSet
        {
            None = 0,
            Clean = 1,
            Defaults = 2,
            All = 3,
        }

        public string GetParametersSignature(Specification spec, bool writeType = true, bool avoidSelfpOut = false, bool pOutAddress = false, ParametersSet writeParamTypes = ParametersSet.All)
        {
            StringBuilder signature = new StringBuilder();
            foreach (var p in Params)
            {
                if (!writeParamTypes.HasFlag(ParametersSet.Defaults) && p.HasDefault)
                    continue;

                if (!writeParamTypes.HasFlag(ParametersSet.Clean) && !p.HasDefault)
                    continue;

                if (p.Name == "...")
                    continue;

                if (avoidSelfpOut && (p.Name == "self" || p.Name == "pOut"))
                    continue;

                if (writeType)
                {
                    string csType = Helpers.ConvertToCSharpType(p.Type, Helpers.Family.param);
                    signature.Append($"{csType} ");
                }

                string address = pOutAddress && p.Name == "pOut" ? "&" : "";
                signature.Append($"{address}{p.Name}");

                if (!writeParamTypes.HasFlag(ParametersSet.Clean)
                    && writeParamTypes.HasFlag(ParametersSet.Defaults))
                {
                    string defaultValue = Helpers.ConvertDefault(p.DefaultValue, p.Type, spec);
                    if (!string.IsNullOrEmpty(defaultValue))
                        signature.Append($" = {defaultValue}");
                }

                signature.Append(", ");
            }

            if(signature.Length > 0
                && !(!writeParamTypes.HasFlag(ParametersSet.Defaults) && Params.Any(p => p.HasDefault)))
                signature.Length -= 2;

            return signature.ToString();
        }

        public Param HasOut()
        {
            return Params.Find(p => p.Name == "pOut");
        }
    }

    public class Param
    {
        public string Name;
        public string Type;
        public string DefaultValue;

        public bool HasDefault
        {
            get => !string.IsNullOrEmpty(DefaultValue);
        }

        public static Param FromJson(JToken elem)
        {
            Param p = new Param();
            p.Name = elem["name"].ToString();

            if (p.Name == "out" ||
                p.Name == "in" ||
                p.Name == "ref")
                p.Name = "@" + p.Name;

            p.Type = elem["type"].ToString();

            return p;
        }
    }
}