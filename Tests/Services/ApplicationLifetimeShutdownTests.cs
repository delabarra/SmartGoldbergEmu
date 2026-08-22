using System;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    [Collection("GameLaunch")]
    public sealed class ApplicationLifetimeShutdownTests : IDisposable
    {
        public ApplicationLifetimeShutdownTests()
        {
            ServiceLocator.ResetApplicationLifetimeForTests();
        }

        public void Dispose()
        {
            ServiceLocator.ResetApplicationLifetimeForTests();
        }

        [Fact]
        public void ApplicationLifetimeToken_is_live_until_cancel()
        {
            CancellationToken token = ServiceLocator.ApplicationLifetimeToken;
            Assert.False(token.IsCancellationRequested);

            ServiceLocator.CancelApplicationLifetime();

            Assert.True(token.IsCancellationRequested);
            Assert.True(ServiceLocator.ApplicationLifetimeToken.IsCancellationRequested);
        }

        [Fact]
        public void CancelApplicationLifetime_is_idempotent()
        {
            ServiceLocator.CancelApplicationLifetime();
            ServiceLocator.CancelApplicationLifetime();
            Assert.True(ServiceLocator.ApplicationLifetimeToken.IsCancellationRequested);
        }

        [Fact]
        public void ResetApplicationLifetimeForTests_restores_uncancelled_token()
        {
            ServiceLocator.CancelApplicationLifetime();
            Assert.True(ServiceLocator.ApplicationLifetimeToken.IsCancellationRequested);

            ServiceLocator.ResetApplicationLifetimeForTests();
            Assert.False(ServiceLocator.ApplicationLifetimeToken.IsCancellationRequested);
        }

        [Fact]
        public void GameLaunchService_CancelPendingRegistryRestores_is_safe_when_empty()
        {
            ServiceLocator.GameLaunchService.CancelPendingRegistryRestores();
            ServiceLocator.GameLaunchService.CancelPendingRegistryRestores();
        }

        [Fact]
        public async Task SteamGameSearchService_honors_precancelled_token()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                SteamGameSearchService.SearchByNameAsync("half-life", cancellationToken: cts.Token));
        }
    }
}
