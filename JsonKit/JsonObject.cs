using System;
using System.Collections;
using System.Collections.Generic;

namespace SmartGoldbergEmu.JsonKit
{
    public sealed class JsonObject : JsonValue, IEnumerable<JsonProperty>
    {
        private readonly Dictionary<string, JsonValue> _properties =
            new Dictionary<string, JsonValue>(StringComparer.Ordinal);

        public JsonObject() { }

        public JsonObject(IEnumerable<KeyValuePair<string, JsonValue>> properties)
        {
            if (properties == null)
                return;
            foreach (var pair in properties)
                _properties[pair.Key] = pair.Value;
        }

        public override JsonValueKind Kind => JsonValueKind.Object;

        public int Count => _properties.Count;

        public new JsonValue this[string key]
        {
            get
            {
                if (key == null)
                    return null;
                return _properties.TryGetValue(key, out JsonValue value) ? value : null;
            }
            set
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key));
                if (value == null || value.Kind == JsonValueKind.Null)
                    _properties.Remove(key);
                else
                    _properties[key] = value;
            }
        }

        public new static JsonObject Parse(string json) => (JsonObject)JsonValue.Parse(json);

        public JsonProperty Property(string name)
        {
            if (name == null || !_properties.TryGetValue(name, out JsonValue value))
                return null;
            return new JsonProperty(name, value);
        }

        public IEnumerable<JsonProperty> Properties()
        {
            foreach (var pair in _properties)
                yield return new JsonProperty(pair.Key, pair.Value);
        }

        public bool Remove(string key) => _properties.Remove(key);

        public override JsonValue DeepClone()
        {
            var clone = new JsonObject();
            foreach (var pair in _properties)
                clone._properties[pair.Key] = pair.Value.DeepClone();
            return clone;
        }

        IEnumerator IEnumerable.GetEnumerator() => Properties().GetEnumerator();

        public IEnumerator<JsonProperty> GetEnumerator() => Properties().GetEnumerator();
    }
}
