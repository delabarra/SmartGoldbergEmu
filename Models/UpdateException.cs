using System;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Exception thrown when emulator update fails.
    /// </summary>
    public class UpdateException : Exception
    {
        public UpdateException(string message) : base(message)
        {
        }

        public UpdateException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}

