using System.Collections;
using System.Collections.Generic;

namespace AppDataKit
{
    internal static class AppInfoJsonConverter
    {
        public static AppInfoKeyValue ToKeyValue(object value)
        {
            if (value == null)
                return null;

            if (value is Dictionary<string, object> obj)
                return ObjectToKeyValue(string.Empty, obj);

            if (value is object[] array)
                return ArrayToKeyValue(string.Empty, array);

            if (value is ArrayList arrayList)
                return ArrayToKeyValue(string.Empty, arrayList);

            if (value is string s)
                return new AppInfoKeyValue(string.Empty, s ?? string.Empty);

            if (value is bool b)
                return new AppInfoKeyValue(string.Empty, b ? "1" : "0");

            return new AppInfoKeyValue(string.Empty, value.ToString());
        }

        public static AppInfoKeyValue ObjectToRootKeyValue(Dictionary<string, object> obj, bool stripLeadingUnderscoreMetadata = true)
        {
            if (obj == null)
                return null;

            var root = new AppInfoKeyValue();
            foreach (KeyValuePair<string, object> prop in obj)
            {
                if (stripLeadingUnderscoreMetadata && prop.Key != null && prop.Key.Length > 0 && prop.Key[0] == '_')
                    continue;
                root.Children.Add(ToKeyValueChild(prop.Key, prop.Value));
            }
            return root;
        }

        private static AppInfoKeyValue ObjectToKeyValue(string name, Dictionary<string, object> obj)
        {
            var kv = new AppInfoKeyValue(name);
            foreach (KeyValuePair<string, object> prop in obj)
                kv.Children.Add(ToKeyValueChild(prop.Key, prop.Value));
            return kv;
        }

        private static AppInfoKeyValue ArrayToKeyValue(string name, object[] array)
        {
            var kv = new AppInfoKeyValue(name);
            for (int i = 0; i < array.Length; i++)
                kv.Children.Add(ToKeyValueChild(i.ToString(), array[i]));
            return kv;
        }

        private static AppInfoKeyValue ArrayToKeyValue(string name, ArrayList array)
        {
            var kv = new AppInfoKeyValue(name);
            for (int i = 0; i < array.Count; i++)
                kv.Children.Add(ToKeyValueChild(i.ToString(), array[i]));
            return kv;
        }

        private static AppInfoKeyValue ToKeyValueChild(string name, object value)
        {
            if (value == null)
                return new AppInfoKeyValue(name, string.Empty);

            if (value is Dictionary<string, object> obj)
                return ObjectToKeyValue(name, obj);

            if (value is object[] arr)
                return ArrayToKeyValue(name, arr);

            if (value is ArrayList arrList)
                return ArrayToKeyValue(name, arrList);

            if (value is string s)
                return new AppInfoKeyValue(name, s ?? string.Empty);

            if (value is bool b)
                return new AppInfoKeyValue(name, b ? "1" : "0");

            return new AppInfoKeyValue(name, value.ToString());
        }
    }
}
