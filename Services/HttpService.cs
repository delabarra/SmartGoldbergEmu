using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Services
{
    public static class HttpServiceFactory
    {
        private static Func<TimeSpan?, IHttpService> _testFactory;

        public static IHttpService Create()
        {
            return Create(null);
        }

        public static IHttpService Create(TimeSpan timeout)
        {
            return Create((TimeSpan?)timeout);
        }

        private static IHttpService Create(TimeSpan? timeout)
        {
            if (_testFactory != null)
                return _testFactory(timeout);

            return timeout.HasValue ? new HttpService(timeout.Value) : new HttpService();
        }

        internal static void SetTestFactoryForTests(Func<TimeSpan?, IHttpService> factory)
        {
            _testFactory = factory;
        }

        internal static void ClearTestFactoryForTests()
        {
            _testFactory = null;
        }
    }

    public class HttpService : IHttpService
    {
        private readonly HttpClient _apiClient;
        private readonly HttpClient _downloadClient;
        private bool _disposed = false;

        public HttpService()
        {
            _apiClient = CreateClient(null, automaticDecompression: true);
            _downloadClient = CreateClient(null, automaticDecompression: false);
        }

        public HttpService(TimeSpan timeout)
        {
            _apiClient = CreateClient(timeout, automaticDecompression: true);
            _downloadClient = CreateClient(timeout, automaticDecompression: false);
        }

        private static HttpClient CreateClient(TimeSpan? timeout, bool automaticDecompression)
        {
            var handler = new HttpClientHandler();
            if (automaticDecompression)
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            var client = new HttpClient(handler);
            if (timeout.HasValue)
                client.Timeout = timeout.Value;
            client.DefaultRequestHeaders.Add("User-Agent", PathConstants.LauncherPerUserFolderName);
            return client;
        }

        public async Task<HttpResponseMessage> GetAsync(string uri, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HttpService));

            return await _apiClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HttpService));

            return await _apiClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        }

        public async Task DownloadFileAsync(string uri, string filePath, Action<double> progressCallback = null, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HttpService));

            using (var response = await _downloadClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;
                
                using (var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var fileStream = System.IO.File.Create(filePath))
                {
                    var buffer = new byte[65536]; // 64KB for better throughput
                    int bytesRead;
                    var lastReportedProgress = 0.0;
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                        downloadedBytes += bytesRead;
                        
                        if (progressCallback != null && totalBytes > 0)
                        {
                            var progress = (double)downloadedBytes / totalBytes;
                            if (progress - lastReportedProgress >= 0.01 || progress >= 1.0)
                            {
                                lastReportedProgress = progress;
                                progressCallback(progress);
                            }
                        }
                    }
                }
            }
        }

        public async Task DownloadFileAsync(Uri uri, string filePath, Action<double> progressCallback = null, CancellationToken cancellationToken = default)
        {
            await DownloadFileAsync(uri.ToString(), filePath, progressCallback, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _apiClient?.Dispose();
                _downloadClient?.Dispose();
                _disposed = true;
            }
        }
    }
}
