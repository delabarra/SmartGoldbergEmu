using System;
using System.Collections;
using System.Collections.Generic;

namespace SmartGoldbergEmu.JsonKit
{
    public sealed class JsonArray : JsonValue, IEnumerable<JsonValue>
    {
        private readonly List<JsonValue> _items = new List<JsonValue>();

        public JsonArray() { }

        public JsonArray(IEnumerable<JsonValue> items)
        {
            if (items == null)
                return;
            foreach (var item in items)
                _items.Add(item);
        }

        public override JsonValueKind Kind => JsonValueKind.Array;

        public int Count => _items.Count;

        public JsonValue this[int index]
        {
            get => _items[index];
            set => _items[index] = value ?? JsonNull.Instance;
        }

        public void Add(JsonValue value) => _items.Add(value ?? JsonNull.Instance);

        public new static JsonArray Parse(string json) => (JsonArray)JsonValue.Parse(json);

        public override JsonValue DeepClone()
        {
            var clone = new JsonArray();
            foreach (var item in _items)
                clone.Add(item.DeepClone());
            return clone;
        }

        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        public IEnumerator<JsonValue> GetEnumerator() => _items.GetEnumerator();
    }
}
