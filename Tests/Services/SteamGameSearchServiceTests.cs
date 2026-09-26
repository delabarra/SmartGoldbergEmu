using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    [Collection("StaticServiceHooks")]
    public sealed class SteamGameSearchServiceTests : IDisposable
    {
        public void Dispose()
        {
            SteamGameSearchService.ResetCatalogEnrichTimeoutForTests();
        }

        [Fact]
        public async Task SearchByNameAsync_returns_store_results_when_vercel_hangs()
        {
            SteamGameSearchService.SetCatalogEnrichTimeoutForTests(TimeSpan.FromMilliseconds(200));

            string term = "portal";
            string storeUrl = string.Format(
                ApplicationConstants.SteamStoreSearchCatalogUrlFormat,
                Uri.EscapeDataString(term));
            string catalogUrl = string.Format(
                ApplicationConstants.SteamSearchGamesApiUrlFormat,
                Uri.EscapeDataString(term));

            string storeHtml =
                "<a class=\"search_result_row\" data-ds-appid=\"400\">" +
                "<div class=\"title\">Portal</div></a>";

            using (var httpScope = new HttpServiceTestScope())
            {
                httpScope.HttpService.SetResponse(storeUrl, () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(storeHtml, Encoding.UTF8, "text/html")
                });
                httpScope.HttpService.SetDelayedResponse(
                    catalogUrl,
                    TimeSpan.FromMinutes(1),
                    () => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[]", Encoding.UTF8, "application/json")
                    });

                var sw = Stopwatch.StartNew();
                List<AppSearchResult> results = await SteamGameSearchService.SearchByNameAsync(term);
                sw.Stop();

                Assert.Contains(results, r => r != null && r.AppId == 400 && r.Name == "Portal");
                Assert.True(
                    sw.Elapsed < TimeSpan.FromSeconds(2),
                    "Hung vercel must not block store results for the full HTTP timeout.");
            }
        }

        [Fact]
        public async Task SearchByNameAsync_reports_store_progress_before_slow_vercel_finishes()
        {
            SteamGameSearchService.SetCatalogEnrichTimeoutForTests(TimeSpan.FromSeconds(2));

            string term = "portal";
            string storeUrl = string.Format(
                ApplicationConstants.SteamStoreSearchCatalogUrlFormat,
                Uri.EscapeDataString(term));
            string catalogUrl = string.Format(
                ApplicationConstants.SteamSearchGamesApiUrlFormat,
                Uri.EscapeDataString(term));

            string storeHtml =
                "<a class=\"search_result_row\" data-ds-appid=\"400\">" +
                "<div class=\"title\">Portal</div></a>";
            // Overlap store AppId so enrichment does not trigger offline steamcmd type checks.
            string catalogJson = "[{\"appid\":400,\"name\":\"Portal\"}]";

            using (var httpScope = new HttpServiceTestScope())
            {
                httpScope.HttpService.SetResponse(storeUrl, () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(storeHtml, Encoding.UTF8, "text/html")
                });
                httpScope.HttpService.SetDelayedResponse(
                    catalogUrl,
                    TimeSpan.FromMilliseconds(800),
                    () => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(catalogJson, Encoding.UTF8, "application/json")
                    });

                var firstStorePaint = new TaskCompletionSource<TimeSpan>();
                var sw = Stopwatch.StartNew();
                var progress = new Progress<IReadOnlyList<AppSearchResult>>(live =>
                {
                    if (live != null && live.Any(r => r != null && r.AppId == 400))
                        firstStorePaint.TrySetResult(sw.Elapsed);
                });

                List<AppSearchResult> results = await SteamGameSearchService.SearchByNameAsync(
                    term,
                    progress: progress);

                TimeSpan paintElapsed = await firstStorePaint.Task;
                Assert.True(
                    paintElapsed < TimeSpan.FromMilliseconds(500),
                    "Store progress must not wait on the delayed vercel response.");
                Assert.Contains(results, r => r != null && r.AppId == 400 && r.Name == "Portal");
            }
        }
    }
}
