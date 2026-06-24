using System.Collections.Generic;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class LaunchSessionCleanupDeployTests
    {
        [Fact]
        public void TryExecuteCleanup_restores_original_dll_from_sge_backup()
        {
            string workDir = TestFileHelper.CreateTempDirectory("sge-cleanup-restore-");
            try
            {
                string targetPath = Path.Combine(workDir, "steam_api64.dll");
                string backupPath = targetPath + PathConstants.SteamApiBackupSidecarExtension;
                File.WriteAllBytes(backupPath, new[] { LaunchDeployTestHarness.OriginalFileMarker });
                File.WriteAllBytes(targetPath, new[] { LaunchDeployTestHarness.GoldbergFileMarker });

                var session = new PersistedLaunchSession
                {
                    AppId = LaunchDeployTestHarness.DefaultTestAppId,
                    GameProcessId = 0,
                    RestoreActiveProcessRegistry = false,
                    DeploySites = new List<PersistedDeploySite>
                    {
                        new PersistedDeploySite
                        {
                            Files = new List<PersistedFileDeployment>
                            {
                                new PersistedFileDeployment
                                {
                                    TargetPath = targetPath,
                                    BackupPath = backupPath,
                                    HadOriginal = true,
                                },
                            },
                        },
                    },
                };

                var cleanup = new LaunchSessionCleanupService(new NullLogService(), new EmulatorConfigService());
                cleanup.TryExecuteCleanup(null, session);

                Assert.Equal(LaunchDeployTestHarness.OriginalFileMarker, File.ReadAllBytes(targetPath)[0]);
                Assert.False(File.Exists(backupPath));
            }
            finally
            {
                try { Directory.Delete(workDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryExecuteCleanup_deletes_deployed_dll_when_no_original_existed()
        {
            string workDir = TestFileHelper.CreateTempDirectory("sge-cleanup-delete-");
            try
            {
                string targetPath = Path.Combine(workDir, "Steam.dll");
                File.WriteAllBytes(targetPath, new[] { LaunchDeployTestHarness.GoldbergFileMarker });

                var session = new PersistedLaunchSession
                {
                    AppId = LaunchDeployTestHarness.DefaultTestAppId,
                    GameProcessId = 0,
                    RestoreActiveProcessRegistry = false,
                    DeploySites = new List<PersistedDeploySite>
                    {
                        new PersistedDeploySite
                        {
                            Files = new List<PersistedFileDeployment>
                            {
                                new PersistedFileDeployment
                                {
                                    TargetPath = targetPath,
                                    BackupPath = targetPath + PathConstants.SteamApiBackupSidecarExtension,
                                    HadOriginal = false,
                                },
                            },
                        },
                    },
                };

                var cleanup = new LaunchSessionCleanupService(new NullLogService(), new EmulatorConfigService());
                cleanup.TryExecuteCleanup(null, session);

                Assert.False(File.Exists(targetPath));
            }
            finally
            {
                try { Directory.Delete(workDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryExecuteCleanup_removes_load_dlls_folder()
        {
            string loadDlls = TestFileHelper.CreateTempDirectory("sge-cleanup-loaddlls-");
            try
            {
                File.WriteAllBytes(Path.Combine(loadDlls, "extra_x64.dll"), new[] { LaunchDeployTestHarness.StagedExtraDllMarker });

                var session = new PersistedLaunchSession
                {
                    AppId = LaunchDeployTestHarness.DefaultTestAppId,
                    GameProcessId = 0,
                    RestoreActiveProcessRegistry = false,
                    LoadDllsFolder = loadDlls,
                };

                var cleanup = new LaunchSessionCleanupService(new NullLogService(), new EmulatorConfigService());
                cleanup.TryExecuteCleanup(null, session);

                Assert.False(Directory.Exists(loadDlls));
            }
            finally
            {
                try { Directory.Delete(loadDlls, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryExecuteCleanup_removes_steamclient_dlls_from_game_library_folder()
        {
            string libraryFolder = TestFileHelper.CreateTempDirectory("sge-cleanup-library-");
            try
            {
                File.WriteAllBytes(Path.Combine(libraryFolder, PathConstants.GoldbergSteamClientDll64), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(libraryFolder, PathConstants.GoldbergGameOverlayRendererDll64), new byte[] { 2 });

                var session = new PersistedLaunchSession
                {
                    AppId = LaunchDeployTestHarness.DefaultTestAppId,
                    GameProcessId = 0,
                    RestoreActiveProcessRegistry = false,
                    GameLibraryFolder = libraryFolder,
                };

                var cleanup = new LaunchSessionCleanupService(new NullLogService(), new EmulatorConfigService());
                cleanup.TryExecuteCleanup(null, session);

                Assert.False(File.Exists(Path.Combine(libraryFolder, PathConstants.GoldbergSteamClientDll64)));
                Assert.False(File.Exists(Path.Combine(libraryFolder, PathConstants.GoldbergGameOverlayRendererDll64)));
            }
            finally
            {
                try { Directory.Delete(libraryFolder, recursive: true); } catch { }
            }
        }

        [Fact]
        public void ReconcileStaleSessionForAppId_cleans_manifest_when_pid_is_not_running()
        {
            using (var harness = new LaunchDeployTestHarness())
            {
                string workDir = TestFileHelper.CreateTempDirectory("sge-stale-deploy-");
                try
                {
                    string targetPath = Path.Combine(workDir, PathConstants.GoldbergSteamDllFileName);
                    string backupPath = targetPath + PathConstants.SteamApiBackupSidecarExtension;
                    File.WriteAllBytes(backupPath, new[] { LaunchDeployTestHarness.OriginalFileMarker });
                    File.WriteAllBytes(targetPath, new[] { LaunchDeployTestHarness.GoldbergFileMarker });

                    var session = new PersistedLaunchSession
                    {
                        AppId = LaunchDeployTestHarness.DefaultTestAppId,
                        GameProcessId = 999999,
                        RestoreActiveProcessRegistry = false,
                        DeploySites = new List<PersistedDeploySite>
                        {
                            new PersistedDeploySite
                            {
                                Files = new List<PersistedFileDeployment>
                                {
                                    new PersistedFileDeployment
                                    {
                                        TargetPath = targetPath,
                                        BackupPath = backupPath,
                                        HadOriginal = true,
                                    },
                                },
                            },
                        },
                    };

                    string manifestPath = harness.CleanupService.WriteSession(session);
                    Assert.True(File.Exists(manifestPath));

                    harness.CleanupService.ReconcileStaleSessionForAppId(LaunchDeployTestHarness.DefaultTestAppId);

                    Assert.Equal(LaunchDeployTestHarness.OriginalFileMarker, File.ReadAllBytes(targetPath)[0]);
                    Assert.False(File.Exists(manifestPath));
                }
                finally
                {
                    try { Directory.Delete(workDir, recursive: true); } catch { }
                }
            }
        }
    }
}
