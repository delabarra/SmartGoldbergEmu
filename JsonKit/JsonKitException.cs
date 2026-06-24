using System;

namespace SmartGoldbergEmu.JsonKit
{
    public class JsonKitException : Exception
    {
        public JsonKitException(string message) : base(message) { }

        public JsonKitException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class JsonReaderException : JsonKitException
    {
        public JsonReaderException(string message) : base(message) { }
    }
}
