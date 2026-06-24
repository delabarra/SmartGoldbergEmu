namespace SmartGoldbergEmu.JsonKit
{
    public sealed class JsonProperty
    {
        public JsonProperty(string name, JsonValue value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public JsonValue Value { get; }
    }
}
