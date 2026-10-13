using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class GameImageServiceDownloadReportTests
    {
        private static GameImageService.AssetUrlAttempt Attempt(string host, double seconds, string failure = null, bool notFound = false, bool timeout = false)
        {
            return new GameImageService.AssetUrlAttempt
            {
                Host = host,
                Elapsed = TimeSpan.FromSeconds(seconds),
                Succeeded = failure == null,
                Failure = failure,
                IsNotFound = notFound,
                IsTimeout = timeout
            };
        }

        private static GameImageService.AssetDownloadResult Result(string fileName, GameImageService.AssetDownloadOutcome outcome, params GameImageService.AssetUrlAttempt[] attempts)
        {
            var result = new GameImageService.AssetDownloadResult { FileName = fileName, Outcome = outcome };
            result.Attempts.AddRange(attempts);
            return result;
        }

        [Fact]
        public void Report_lists_counts_failure_reasons_404s_and_slow_hosts()
        {
            var results = new List<GameImageService.AssetDownloadResult>
            {
                Result("header.jpg", GameImageService.AssetDownloadOutcome.Downloaded, Attempt("cdn.a", 0.2)),
                Result("logo.png", GameImageService.AssetDownloadOutcome.AlreadyPresent),
                Result("capsule_467x181.jpg", GameImageService.AssetDownloadOutcome.NotFound,
                    Attempt("cdn.a", 0.3, "HTTP 404 (Not Found)", notFound: true)),
                Result("logo_schinese_2x.png", GameImageService.AssetDownloadOutcome.Failed,
                    Attempt("shared.slow", 30, "timed out after 30.0s", timeout: true),
                    Attempt("cdn.a", 0.2, "HTTP 404 (Not Found)", notFound: true))
            };

            var lines = GameImageService.BuildAssetDownloadReport(2893570, 2893570, results, TimeSpan.FromSeconds(48.25));

            Assert.Equal(4, lines.Count);
            Assert.Equal(LogLevel.Warning, lines[0].Key);
            Assert.Contains("4 requested in 48.3s", lines[0].Value);
            Assert.Contains("1 downloaded, 1 already on disk, 1 not on CDN, 1 failed", lines[0].Value);
            Assert.Contains("logo_schinese_2x.png [shared.slow: timed out after 30.0s; cdn.a: HTTP 404 (Not Found)]", lines[1].Value);
            Assert.Equal(LogLevel.Info, lines[2].Key);
            Assert.Contains("capsule_467x181.jpg", lines[2].Value);
            Assert.Contains("shared.slow 1 timed out, 0 over 10s (of 1 requests)", lines[3].Value);
        }

        [Fact]
        public void Report_is_single_info_line_when_everything_succeeds()
        {
            var results = new List<GameImageService.AssetDownloadResult>
            {
                Result("header.jpg", GameImageService.AssetDownloadOutcome.Downloaded, Attempt("cdn.a", 0.2))
            };

            var lines = GameImageService.BuildAssetDownloadReport(10, 20, results, TimeSpan.FromSeconds(1));

            Assert.Single(lines);
            Assert.Equal(LogLevel.Info, lines[0].Key);
            Assert.Contains("AppId 10 (Steam AppId 20)", lines[0].Value);
        }

        [Fact]
        public void DescribeDownloadFailure_reports_status_timeout_and_inner_network_error()
        {
            Assert.Equal("HTTP 404 (Not Found)", HttpHelpers.DescribeDownloadFailure(
                new HttpStatusException(HttpStatusCode.NotFound, "Not Found"), TimeSpan.Zero));
            Assert.Equal("timed out after 30.0s", HttpHelpers.DescribeDownloadFailure(
                new TaskCanceledException(), TimeSpan.FromSeconds(30)));

            var dns = new System.Net.Http.HttpRequestException(
                "An error occurred while sending the request.",
                new WebException("The remote name could not be resolved", WebExceptionStatus.NameResolutionFailure));
            Assert.StartsWith("NameResolutionFailure:", HttpHelpers.DescribeDownloadFailure(dns, TimeSpan.Zero));
            Assert.True(HttpHelpers.IsNotFound(new HttpStatusException(HttpStatusCode.NotFound, "Not Found")));
        }
    }
}
