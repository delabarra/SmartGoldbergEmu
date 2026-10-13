using System.Net;
using System.Net.Http;

namespace SmartGoldbergEmu.Models
{
    // HttpRequestException subtype so callers that catch HttpRequestException keep working; net48 has no StatusCode on the base type.
    public class HttpStatusException : HttpRequestException
    {
        public HttpStatusCode StatusCode { get; }

        public HttpStatusException(HttpStatusCode statusCode, string reasonPhrase)
            : base($"HTTP {(int)statusCode} ({reasonPhrase})")
        {
            StatusCode = statusCode;
        }
    }
}
