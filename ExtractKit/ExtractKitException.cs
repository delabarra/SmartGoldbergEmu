using System;

namespace SmartGoldbergEmu.ExtractKit
{
    public sealed class ExtractKitException : Exception
    {
        public ExtractKitException(string message)
            : base(message)
        {
        }

        public ExtractKitException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
