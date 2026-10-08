using System;

namespace SmartGoldbergEmu.Models
{
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

