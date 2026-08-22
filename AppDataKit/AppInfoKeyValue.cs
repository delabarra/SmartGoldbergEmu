using System;
using System.Collections.Generic;
using System.Text;

namespace AppDataKit
{
    /// <summary>Valve KeyValues tree for steamcmd appinfo payloads.</summary>
    public class AppInfoKeyValue
    {
        private const int MaxParseDepth = 64;

        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public List<AppInfoKeyValue> Children { get; } = new List<AppInfoKeyValue>();

        public AppInfoKeyValue() { }
        public AppInfoKeyValue(string name, string value = "") { Name = name; Value = value; }

        public AppInfoKeyValue GetChild(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            foreach (AppInfoKeyValue child in Children)
            {
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                    return child;
            }
            return null;
        }

        public string ToVdf()
        {
            var sb = new StringBuilder();
            WriteVdf(sb, 0);
            return sb.ToString();
        }

        private void WriteVdf(StringBuilder sb, int depth)
        {
            string indent = new string('\t', depth);
            if (Children.Count == 0)
            {
                sb.Append(indent).Append('"').Append(EscapeVdf(Name)).Append("\"\t\"")
                  .Append(EscapeVdf(Value)).Append("\"\n");
                return;
            }

            sb.Append(indent).Append('"').Append(EscapeVdf(Name)).Append("\"\n");
            sb.Append(indent).Append("{\n");
            foreach (var child in Children)
                child.WriteVdf(sb, depth + 1);
            sb.Append(indent).Append("}\n");
        }

        private static string EscapeVdf(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
