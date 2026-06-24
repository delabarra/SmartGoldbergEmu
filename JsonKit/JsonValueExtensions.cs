using System;
using System.Globalization;

namespace SmartGoldbergEmu.JsonKit
{
    public static class JsonValueExtensions
    {
        public static bool ToBoolean(this JsonValue token)
        {
            if (token == null)
                return false;
            if (token.Kind == JsonValueKind.Boolean)
                return ((JsonBool)token).Value;
            if (token.TryGetInt64(out long l))
                return l != 0;
            if (token.Kind == JsonValueKind.Float)
                return ((JsonNumber)token).FloatValue != 0;
            string text = token.ToString().Trim();
            return text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static int ToInt32(this JsonValue token)
        {
            if (token == null)
                return 0;
            if (token.TryGetInt64(out long l))
                return (int)l;
            int parsed;
            return int.TryParse(token.ToString().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : 0;
        }

        public static long ToInt64(this JsonValue token)
        {
            if (token == null)
                return 0;
            if (token.TryGetInt64(out long l))
                return l;
            long parsed;
            return long.TryParse(token.ToString().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : 0;
        }

        public static double ToDouble(this JsonValue token)
        {
            if (token == null)
                return 0;
            if (token.Kind == JsonValueKind.Float)
                return ((JsonNumber)token).FloatValue;
            if (token.TryGetInt64(out long l))
                return l;
            double parsed;
            return double.TryParse(token.ToString().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : 0;
        }

        // Compatibility with prior JToken.ToObject<T>() call sites.
        public static T ToObject<T>(this JsonValue token)
        {
            if (typeof(T) == typeof(bool))
                return (T)(object)token.ToBoolean();
            if (typeof(T) == typeof(int))
                return (T)(object)token.ToInt32();
            if (typeof(T) == typeof(long))
                return (T)(object)token.ToInt64();
            if (typeof(T) == typeof(double))
                return (T)(object)token.ToDouble();
            if (typeof(T) == typeof(string))
                return (T)(object)(token?.ToString() ?? string.Empty);
            throw new NotSupportedException("ToObject is not supported for type " + typeof(T).FullName);
        }
    }
}
