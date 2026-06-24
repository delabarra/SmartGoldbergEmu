using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class LaunchSessionManifestTests
    {
        [Fact]
        public void Manifest_round_trips_deploy_sites_and_app_id()
        {
            var session = new PersistedLaunchSession
            {
                AppId = 562860,
                GameProcessId = 1234,
                RestoreActiveProcessRegistry = true,
                GameLibraryFolder = @"C:\SGE\games\562860",
                LoadDllsFolder = @"C:\SGE\games\562860\steam_settings\load_dlls",
                DeploySites = new System.Collections.Generic.List<PersistedDeploySite>
                {
                    new PersistedDeploySite
                    {
                        MirroredSteamSettingsPath = @"D:\Games\Foo\steam_settings",
                        Files = new System.Collections.Generic.List<PersistedFileDeployment>
                        {
                            new PersistedFileDeployment
                            {
                                TargetPath = @"D:\Games\Foo\steam_api64.dll",
                                BackupPath = @"D:\Games\Foo\steam_api64.dll.sge",
                                HadOriginal = true,
                            },
                        },
                    },
                },
            };

            var cleanupService = new LaunchSessionCleanupService(new NullLogService(), new EmulatorConfigService());
            string path = cleanupService.WriteSession(session);
            Assert.False(string.IsNullOrEmpty(path));
            try
            {
                Assert.True(cleanupService.TryLoad(path, out PersistedLaunchSession loaded));
                Assert.Equal(session.AppId, loaded.AppId);
                Assert.Equal(session.GameProcessId, loaded.GameProcessId);
                Assert.NotNull(loaded.DeploySites);
                Assert.Single(loaded.DeploySites);
                Assert.NotNull(loaded.DeploySites[0].Files);
                Assert.Single(loaded.DeploySites[0].Files);
                Assert.Equal(@"D:\Games\Foo\steam_api64.dll", loaded.DeploySites[0].Files[0].TargetPath);
                Assert.True(loaded.DeploySites[0].Files[0].HadOriginal);
            }
            finally
            {
                cleanupService.TryDeleteManifest(path);
            }
        }

        [Fact]
        public void TryRestoreRegistryAfterLoadWindow_clears_registry_flags_in_manifest()
        {
            var session = new PersistedLaunchSession
            {
                AppId = 562861,
                GameProcessId = 5678,
                SessionGeneration = "test-generation",
                RestoreActiveProcessRegistry = true,
                SourceModRestore = new PersistedSourceModRestore
                {
                    HadPreviousValue = false,
                },
            };

            var cleanupService = new LaunchSessionCleanupService(new NullLogService(), new EmulatorConfigService());
            string path = cleanupService.WriteSession(session);
            Assert.False(string.IsNullOrEmpty(path));
            try
            {
                cleanupService.TryRestoreRegistryAfterLoadWindow(path, "test-generation", 5678);

                Assert.True(cleanupService.TryLoad(path, out PersistedLaunchSession loaded));
                Assert.False(loaded.RestoreActiveProcessRegistry);
                Assert.Null(loaded.SourceModRestore);
            }
            finally
            {
                cleanupService.TryDeleteManifest(path);
            }
        }

        [Fact]
        public void TryRestoreRegistryAfterLoadWindow_skips_when_generation_mismatch()
        {
            var session = new PersistedLaunchSession
            {
                AppId = 562862,
                GameProcessId = 9012,
                SessionGeneration = "current-generation",
                RestoreActiveProcessRegistry = true,
            };

            var cleanupService = new LaunchSessionCleanupService(new NullLogService(), new EmulatorConfigService());
            string path = cleanupService.WriteSession(session);
            Assert.False(string.IsNullOrEmpty(path));
            try
            {
                cleanupService.TryRestoreRegistryAfterLoadWindow(path, "stale-generation", 9012);

                Assert.True(cleanupService.TryLoad(path, out PersistedLaunchSession loaded));
                Assert.True(loaded.RestoreActiveProcessRegistry);
            }
            finally
            {
                cleanupService.TryDeleteManifest(path);
            }
        }
    }
}
