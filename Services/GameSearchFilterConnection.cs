using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AppDataKit;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Extensions;

namespace SmartGoldbergEmu.Services
{
    // steamcmd + Store HTTP client for unidentified-app type checks.
    // Start when GameSearchForm opens; dispose on close or cancel.
    public sealed class GameSearchFilterConnection : IDisposable
    {
        private readonly HttpClient _http;
        private readonly CancellationTokenSource _lifetimeCts;
        private readonly Task _warmup;
        private bool _disposed;

        public HttpClient Http => _http;

        private GameSearchFilterConnection(HttpClient http, CancellationTokenSource lifetimeCts, Task warmup)
        {
            _http = http;
            _lifetimeCts = lifetimeCts;
            _warmup = warmup;
        }

        public static GameSearchFilterConnection Start(TimeSpan timeout)
        {
            var http = new HttpClient { Timeout = timeout };
            var lifetimeCts = new CancellationTokenSource();
            Task warmup = WarmHostsAsync(http, lifetimeCts.Token)
                .ForgetFaults(ServiceLocator.LogService, nameof(GameSearchFilterConnection));
            return new GameSearchFilterConnection(http, lifetimeCts, warmup);
        }

        public async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            if (_warmup == null || _warmup.IsCompleted)
            {
                await ObserveWarmupAsync().ConfigureAwait(false);
                return;
            }

            if (!cancellationToken.CanBeCanceled)
            {
                await ObserveWarmupAsync().ConfigureAwait(false);
                return;
            }

            var cancelTcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => cancelTcs.TrySetCanceled()))
            {
                Task completed = await Task.WhenAny(_warmup, cancelTcs.Task).ConfigureAwait(false);
                if (completed != _warmup)
                    cancellationToken.ThrowIfCancellationRequested();
                await ObserveWarmupAsync().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            try
            {
                _lifetimeCts.Cancel();
            }
            catch
            {
            }

            _http.Dispose();
            _lifetimeCts.Dispose();
        }

        private async Task ObserveWarmupAsync()
        {
            try
            {
                await _warmup.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Warmup failed; type checks still use this client.
            }
        }

        private static async Task WarmHostsAsync(HttpClient http, CancellationToken cancellationToken)
        {
            string storeOrigin = new Uri(
                string.Format(ApplicationConstants.SteamStoreSearchCatalogUrlFormat, "x"))
                .GetLeftPart(UriPartial.Authority) + "/";

            await Task.WhenAll(
                WarmUrlAsync(http, AppSnapshotOptions.DefaultSteamCmdInfoUrl, cancellationToken),
                WarmUrlAsync(http, storeOrigin, cancellationToken)).ConfigureAwait(false);
        }

        private static async Task WarmUrlAsync(HttpClient http, string url, CancellationToken cancellationToken)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                using (HttpResponseMessage response = await http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }
    }
}
