using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace SmartGoldbergEmu.JsonKit
{
    internal static class JsonWriter
    {
        public static string WriteValue(JsonValue value, JsonFormatting formatting)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, formatting, 0);
            return sb.ToString();
        }

        public static string SerializeObject(object value, JsonFormatting formatting)
        {
            var sb = new StringBuilder();
            WriteObjectGraph(sb, value, formatting, 0);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, JsonValue value, JsonFormatting formatting, int depth)
        {
            if (value == null || value.Kind == JsonValueKind.Null)
            {
                sb.Append("null");
                return;
            }

            switch (value.Kind)
            {
                case JsonValueKind.String:
                    WriteEscapedString(sb, ((JsonString)value).Value);
                    break;
                case JsonValueKind.Integer:
                    sb.Append(((JsonNumber)value).IntegerValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case JsonValueKind.Float:
                    sb.Append(((JsonNumber)value).FloatValue.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case JsonValueKind.Boolean:
                    sb.Append(((JsonBool)value).Value ? "true" : "false");
                    break;
                case JsonValueKind.Array:
                    WriteArray(sb, (JsonArray)value, formatting, depth);
                    break;
                case JsonValueKind.Object:
                    WriteObject(sb, (JsonObject)value, formatting, depth);
                    break;
            }
        }

        private static void WriteObject(StringBuilder sb, JsonObject obj, JsonFormatting formatting, int depth)
        {
            bool indent = formatting == JsonFormatting.Indented;
            sb.Append('{');
            bool first = true;
            foreach (var prop in obj.Properties())
            {
                if (!first)
                    sb.Append(',');
                first = false;

                if (indent)
                {
                    sb.AppendLine();
                    sb.Append(new string(' ', (depth + 1) * 2));
                }

                WriteEscapedString(sb, prop.Name);
                sb.Append(':');
                if (indent)
                    sb.Append(' ');
                WriteValue(sb, prop.Value, formatting, depth + 1);
            }

            if (indent && !first)
            {
                sb.AppendLine();
                sb.Append(new string(' ', depth * 2));
            }
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, JsonArray array, JsonFormatting formatting, int depth)
        {
            bool indent = formatting == JsonFormatting.Indented;
            sb.Append('[');
            bool first = true;
            foreach (var item in array)
            {
                if (!first)
                    sb.Append(',');
                first = false;

                if (indent)
                {
                    sb.AppendLine();
                    sb.Append(new string(' ', (depth + 1) * 2));
                }

                WriteValue(sb, item, formatting, depth + 1);
            }

            if (indent && !first)
            {
                sb.AppendLine();
                sb.Append(new string(' ', depth * 2));
            }
            sb.Append(']');
        }

        private static void WriteObjectGraph(StringBuilder sb, object value, JsonFormatting formatting, int depth)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            if (value is JsonValue jsonValue)
            {
                WriteValue(sb, jsonValue, formatting, depth);
                return;
            }

            if (value is string s)
            {
                WriteEscapedString(sb, s);
                return;
            }

            if (value is bool b)
            {
                sb.Append(b ? "true" : "false");
                return;
            }

            if (value is int i)
            {
                sb.Append(i.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is long l)
            {
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is uint ui)
            {
                sb.Append(ui.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is ulong ul)
            {
                sb.Append(ul.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is float f)
            {
                sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is double d)
            {
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary dict)
            {
                WriteDictionary(sb, dict, formatting, depth);
                return;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                WriteEnumerable(sb, enumerable, formatting, depth);
                return;
            }

            WriteReflectionObject(sb, value, formatting, depth);
        }

        private static void WriteEnumerable(StringBuilder sb, IEnumerable enumerable, JsonFormatting formatting, int depth)
        {
            bool indent = formatting == JsonFormatting.Indented;
            sb.Append('[');
            bool first = true;
            foreach (var item in enumerable)
            {
                if (!first)
                    sb.Append(',');
                first = false;

                if (indent)
                {
                    sb.AppendLine();
                    sb.Append(new string(' ', (depth + 1) * 2));
                }

                WriteObjectGraph(sb, item, formatting, depth + 1);
            }

            if (indent && !first)
            {
                sb.AppendLine();
                sb.Append(new string(' ', depth * 2));
            }
            sb.Append(']');
        }

        private static void WriteDictionary(StringBuilder sb, IDictionary dict, JsonFormatting formatting, int depth)
        {
            bool indent = formatting == JsonFormatting.Indented;
            sb.Append('{');
            bool first = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!first)
                    sb.Append(',');
                first = false;

                if (indent)
                {
                    sb.AppendLine();
                    sb.Append(new string(' ', (depth + 1) * 2));
                }

                WriteEscapedString(sb, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                sb.Append(':');
                if (indent)
                    sb.Append(' ');
                WriteObjectGraph(sb, entry.Value, formatting, depth + 1);
            }

            if (indent && !first)
            {
                sb.AppendLine();
                sb.Append(new string(' ', depth * 2));
            }
            sb.Append('}');
        }

        private static void WriteReflectionObject(StringBuilder sb, object value, JsonFormatting formatting, int depth)
        {
            bool indent = formatting == JsonFormatting.Indented;
            sb.Append('{');
            bool first = true;
            foreach (var prop in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanRead)
                    continue;

                object propValue;
                try
                {
                    propValue = prop.GetValue(value, null);
                }
                catch
                {
                    continue;
                }

                if (!first)
                    sb.Append(',');
                first = false;

                if (indent)
                {
                    sb.AppendLine();
                    sb.Append(new string(' ', (depth + 1) * 2));
                }

                WriteEscapedString(sb, prop.Name);
                sb.Append(':');
                if (indent)
                    sb.Append(' ');
                WriteObjectGraph(sb, propValue, formatting, depth + 1);
            }

            if (indent && !first)
            {
                sb.AppendLine();
                sb.Append(new string(' ', depth * 2));
            }
            sb.Append('}');
        }

        private static void WriteEscapedString(StringBuilder sb, string value)
        {
            sb.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                foreach (char c in value)
                {
                    switch (c)
                    {
                        case '"':
                            sb.Append("\\\"");
                            break;
                        case '\\':
                            sb.Append("\\\\");
                            break;
                        case '\b':
                            sb.Append("\\b");
                            break;
                        case '\f':
                            sb.Append("\\f");
                            break;
                        case '\n':
                            sb.Append("\\n");
                            break;
                        case '\r':
                            sb.Append("\\r");
                            break;
                        case '\t':
                            sb.Append("\\t");
                            break;
                        default:
                            if (c < 32)
                                sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else
                                sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
        }
    }
}
