using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SmartGoldbergEmu.Abstractions
{
    public interface IHttpService : IDisposable
    {
        Task<HttpResponseMessage> GetAsync(string uri, CancellationToken cancellationToken = default);
        Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cancellationToken = default);
        Task DownloadFileAsync(string uri, string filePath, Action<double> progressCallback = null, CancellationToken cancellationToken = default);
        Task DownloadFileAsync(Uri uri, string filePath, Action<double> progressCallback = null, CancellationToken cancellationToken = default);
    }
}
