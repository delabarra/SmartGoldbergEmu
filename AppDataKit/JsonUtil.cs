using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace AppDataKit
{
    internal static class JsonUtil
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static object Parse(string json)
        {
            return Serializer.DeserializeObject(json);
        }

        public static string Serialize(object value)
        {
            return Serializer.Serialize(value);
        }

        public static Dictionary<string, object> AsObject(object value)
        {
            return value as Dictionary<string, object>;
        }

        // Accepts JSON numbers or numeric strings (Steam returns "percent" as a string).
        public static double? ReadOptionalDouble(object value)
        {
            if (value == null)
                return null;
            if (value is IConvertible && !(value is string) && !(value is bool))
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return parsed;
            return null;
        }
    }
}
