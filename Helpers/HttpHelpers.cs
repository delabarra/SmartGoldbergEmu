using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    public static class HttpHelpers
    {
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

        // Invariant culture so log lines read the same on every locale ("48.3s", not "48,3s").
        public static string FormatSeconds(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        public static bool IsNotFound(Exception ex)
        {
            return ex is HttpStatusException status && status.StatusCode == HttpStatusCode.NotFound;
        }

        // HttpClient reports its own Timeout as TaskCanceledException; callers must not pass a cancelled token.
        public static bool IsTimeout(Exception ex)
        {
            return ex is TaskCanceledException
                || (ex is WebException web && web.Status == WebExceptionStatus.Timeout);
        }

        // Short reason for the log: status code, timeout, or the innermost network error (DNS, TLS, connection reset).
        public static string DescribeDownloadFailure(Exception ex, TimeSpan elapsed)
        {
            if (ex == null)
                return "unknown error";
            if (ex is HttpStatusException status)
                return status.Message;
            if (IsTimeout(ex))
                return "timed out after " + FormatSeconds(elapsed);

            Exception innermost = ex;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;

            if (innermost is WebException web && web.Status != WebExceptionStatus.UnknownError)
                return $"{web.Status}: {web.Message}";
            if (innermost is IOException && !(ex is HttpRequestException))
                return "file error: " + innermost.Message;
            return $"{innermost.GetType().Name}: {innermost.Message}";
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
    }
}
