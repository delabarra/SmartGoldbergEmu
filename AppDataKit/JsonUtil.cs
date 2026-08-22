using System.Collections.Generic;
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
    }
}
