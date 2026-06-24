using System;
using System.Net.Http;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    public static class HttpHelpers
    {
        public static async Task<string> GetStringWithRetryAsync(string url, int maxRetries, int delayMs, int timeoutSeconds)
        {
            using (IHttpService service = HttpServiceFactory.Create(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        using (var response = await service.GetAsync(url).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();
                            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        }
                    }
                    catch (Exception)
                    {
                        if (attempt < maxRetries - 1)
                        {
                            await Task.Delay(delayMs).ConfigureAwait(false);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }

            throw new HttpRequestException("Failed to get response after retries");
        }

        public static async Task<byte[]> GetByteArrayAsync(string url, int timeoutSeconds)
        {
            using (IHttpService service = HttpServiceFactory.Create(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                using (var response = await service.GetAsync(url).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
