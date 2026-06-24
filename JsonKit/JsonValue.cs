using System;
using System.Globalization;

namespace SmartGoldbergEmu.JsonKit
{
    public abstract class JsonValue
    {
        public abstract JsonValueKind Kind { get; }

        // Compatibility with prior JToken.Type checks.
        public JsonValueKind Type => Kind;

        public static JsonValue Parse(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));
            return new JsonParser(json).ParseValue();
        }

        public JsonValue this[string key]
        {
            get
            {
                var obj = this as JsonObject;
                return obj == null ? null : obj[key];
            }
        }

        public abstract JsonValue DeepClone();

        public string ToJsonString(JsonFormatting formatting = JsonFormatting.None)
        {
            return JsonWriter.WriteValue(this, formatting);
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case JsonValueKind.String:
                    return ((JsonString)this).Value;
                case JsonValueKind.Integer:
                    return ((JsonNumber)this).IntegerValue.ToString(CultureInfo.InvariantCulture);
                case JsonValueKind.Float:
                    return ((JsonNumber)this).FloatValue.ToString(CultureInfo.InvariantCulture);
                case JsonValueKind.Boolean:
                    return ((JsonBool)this).Value ? bool.TrueString : bool.FalseString;
                case JsonValueKind.Null:
                    return string.Empty;
                default:
                    return ToJsonString();
            }
        }

        public bool TryGetInt64(out long value)
        {
            value = 0;
            if (Kind == JsonValueKind.Integer)
            {
                value = ((JsonNumber)this).IntegerValue;
                return true;
            }
            if (Kind == JsonValueKind.Float)
            {
                value = (long)((JsonNumber)this).FloatValue;
                return true;
            }
            return false;
        }

        public long ToInt64()
        {
            if (TryGetInt64(out long v))
                return v;
            throw new InvalidOperationException("Token is not a number.");
        }
    }

    public sealed class JsonNull : JsonValue
    {
        public static readonly JsonNull Instance = new JsonNull();

        private JsonNull() { }

        public override JsonValueKind Kind => JsonValueKind.Null;

        public override JsonValue DeepClone() => Instance;
    }

    public sealed class JsonBool : JsonValue
    {
        public JsonBool(bool value) => Value = value;

        public bool Value { get; }

        public override JsonValueKind Kind => JsonValueKind.Boolean;

        public override JsonValue DeepClone() => new JsonBool(Value);
    }

    public sealed class JsonString : JsonValue
    {
        public JsonString(string value) => Value = value ?? string.Empty;

        public string Value { get; }

        public override JsonValueKind Kind => JsonValueKind.String;

        public override JsonValue DeepClone() => new JsonString(Value);
    }

    public sealed class JsonNumber : JsonValue
    {
        public JsonNumber(long value)
        {
            IntegerValue = value;
            IsInteger = true;
        }

        public JsonNumber(double value)
        {
            FloatValue = value;
            IsInteger = false;
        }

        public bool IsInteger { get; }
        public long IntegerValue { get; }
        public double FloatValue { get; }

        public override JsonValueKind Kind => IsInteger ? JsonValueKind.Integer : JsonValueKind.Float;

        public override JsonValue DeepClone() =>
            IsInteger ? (JsonValue)new JsonNumber(IntegerValue) : new JsonNumber(FloatValue);
    }
}
