using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    public static class HttpHelpers
    {
        public const int DefaultMaxConcurrentDownloads = 8;

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

        // Stream to a temp file then rename so failed downloads do not leave a partial final path.
        public static async Task DownloadFileAtomicAsync(IHttpService service, string url, string finalPath)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL is required.", nameof(url));
            if (string.IsNullOrEmpty(finalPath))
                throw new ArgumentException("Path is required.", nameof(finalPath));

            string tempPath = finalPath + ".download";
            try
            {
                await service.DownloadFileAsync(url, tempPath).ConfigureAwait(false);
                if (File.Exists(finalPath))
                    File.Delete(finalPath);
                File.Move(tempPath, finalPath);
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                }

                throw;
            }
        }

        public static async Task ForEachBoundedAsync<T>(
            IEnumerable<T> items,
            int maxConcurrency,
            Func<T, Task> action)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (maxConcurrency < 1)
                maxConcurrency = 1;

            using (var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency))
            {
                var tasks = new List<Task>();
                foreach (T item in items)
                {
                    T captured = item;
                    tasks.Add(RunBoundedItemAsync(gate, captured, action));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        public static string FormatByteSize(long bytes)
        {
            if (bytes < 0)
                bytes = 0;

            string unit;
            string format;
            double divisor;
            ResolveByteSizeUnit(bytes, out divisor, out unit, out format);
            return (bytes / divisor).ToString(format) + " " + unit;
        }

        // Shared unit from the larger value: "12.3 / 45.0 MB".
        public static string FormatByteSizeRange(long completedBytes, long totalBytes)
        {
            if (completedBytes < 0)
                completedBytes = 0;
            if (totalBytes < 0)
                totalBytes = 0;
            if (completedBytes > totalBytes && totalBytes > 0)
                completedBytes = totalBytes;

            long unitSource = totalBytes > completedBytes ? totalBytes : completedBytes;
            string unit;
            string format;
            double divisor;
            ResolveByteSizeUnit(unitSource, out divisor, out unit, out format);
            return (completedBytes / divisor).ToString(format)
                + " / "
                + (totalBytes / divisor).ToString(format)
                + " "
                + unit;
        }

        private static void ResolveByteSizeUnit(long bytes, out double divisor, out string unit, out string format)
        {
            const double kb = 1024.0;
            const double mb = kb * 1024.0;
            const double gb = mb * 1024.0;

            if (bytes >= gb)
            {
                divisor = gb;
                unit = "GB";
                format = "0.00";
                return;
            }

            if (bytes >= mb)
            {
                divisor = mb;
                unit = "MB";
                format = "0.0";
                return;
            }

            if (bytes >= kb)
            {
                divisor = kb;
                unit = "KB";
                format = "0.0";
                return;
            }

            divisor = 1.0;
            unit = "B";
            format = "0";
        }

        private static async Task RunBoundedItemAsync<T>(
            SemaphoreSlim gate,
            T item,
            Func<T, Task> action)
        {
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await action(item).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
