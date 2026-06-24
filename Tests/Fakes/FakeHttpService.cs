using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;

namespace SmartGoldbergEmu.Tests.Fakes
{
    internal sealed class FakeHttpService : IHttpService
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _getResponses =
            new Dictionary<string, Func<HttpResponseMessage>>(StringComparer.OrdinalIgnoreCase);

        public void SetJsonResponse(string url, string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _getResponses[url] = () => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        public void SetResponse(string url, Func<HttpResponseMessage> factory)
        {
            _getResponses[url] = factory;
        }

        public Task<HttpResponseMessage> GetAsync(string uri, CancellationToken cancellationToken = default)
        {
            if (_getResponses.TryGetValue(uri, out Func<HttpResponseMessage> factory))
                return Task.FromResult(factory());

            throw new InvalidOperationException("No fake HTTP response configured for: " + uri);
        }

        public Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            return GetAsync(uri.ToString(), cancellationToken);
        }

        public Task DownloadFileAsync(string uri, string filePath, Action<double> progressCallback = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("FakeHttpService does not support downloads.");
        }

        public Task DownloadFileAsync(Uri uri, string filePath, Action<double> progressCallback = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("FakeHttpService does not support downloads.");
        }

        public void Dispose()
        {
        }
    }
}
