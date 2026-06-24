using System;

namespace SmartGoldbergEmu.Models
{
    public class ConnectionException : Exception
    {
        public string Url { get; }
        
        public ConnectionException(string message, string url, Exception innerException) 
            : base(message, innerException)
        {
            Url = url;
        }
    }

    public class RequestTimeoutException : Exception
    {
        public string Url { get; }
        
        public RequestTimeoutException(string url) 
            : base($"Request timed out: {url}")
        {
            Url = url;
        }
    }

    public class AchievementException : Exception
    {
        public string AppId { get; }
        
        public AchievementException(string message, string appId, Exception innerException) 
            : base(message, innerException)
        {
            AppId = appId;
        }
    }

    public class InvalidApiKeyException : Exception
    {
        public InvalidApiKeyException(string message) : base(message) { }
    }

    public class NetworkException : Exception
    {
        public NetworkException(string message) : base(message) { }
    }

    public class AchievementApiException : Exception
    {
        public int StatusCode { get; }
        public string AppId { get; }
        
        public AchievementApiException(string message, int statusCode, string appId) 
            : base(message)
        {
            StatusCode = statusCode;
            AppId = appId;
        }
    }
}

