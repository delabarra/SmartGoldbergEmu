using System;
using System.IO;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class LauncherUpdateManifestTrustHelperTests
    {
        [Fact]
        public void ValidateOrThrow_accepts_work_root_under_install_with_main_exe()
        {
            string installRoot = TestFileHelper.CreateTempDirectory("sge-update-trust-install-");
            try
            {
                File.WriteAllText(
                    Path.Combine(installRoot, LauncherUpdateManifestTrustHelper.LauncherMainExecutableFileName),
                    string.Empty);
                string workRoot = Path.Combine(installRoot, "temp", "launcher-update");
                Directory.CreateDirectory(workRoot);
                string stageRoot = Path.Combine(workRoot, "extracted");
                Directory.CreateDirectory(stageRoot);
                string manifestPath = Path.Combine(workRoot, "apply-manifest.json");
                File.WriteAllText(manifestPath, "{}");

                LauncherUpdateManifestTrustHelper.ValidateOrThrow(
                    manifestPath,
                    installRoot,
                    stageRoot,
                    Path.Combine(installRoot, LauncherUpdateManifestTrustHelper.LauncherMainExecutableFileName),
                    workRoot,
                    workRoot);
            }
            finally
            {
                try { Directory.Delete(installRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public void ValidateOrThrow_rejects_exe_outside_install_root()
        {
            string installRoot = TestFileHelper.CreateTempDirectory("sge-update-trust-badexe-");
            try
            {
                File.WriteAllText(
                    Path.Combine(installRoot, LauncherUpdateManifestTrustHelper.LauncherMainExecutableFileName),
                    string.Empty);
                string workRoot = Path.Combine(installRoot, "temp", "launcher-update");
                Directory.CreateDirectory(workRoot);
                string stageRoot = Path.Combine(workRoot, "extracted");
                Directory.CreateDirectory(stageRoot);
                string manifestPath = Path.Combine(workRoot, "apply-manifest.json");
                File.WriteAllText(manifestPath, "{}");
                string evilExe = Path.Combine(Path.GetTempPath(), "evil.exe");

                Assert.Throws<InvalidOperationException>(() =>
                    LauncherUpdateManifestTrustHelper.ValidateOrThrow(
                        manifestPath,
                        installRoot,
                        stageRoot,
                        evilExe,
                        workRoot,
                        workRoot));
            }
            finally
            {
                try { Directory.Delete(installRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public void ValidateOrThrow_rejects_stage_outside_work_root()
        {
            string installRoot = TestFileHelper.CreateTempDirectory("sge-update-trust-stage-");
            try
            {
                File.WriteAllText(
                    Path.Combine(installRoot, LauncherUpdateManifestTrustHelper.LauncherMainExecutableFileName),
                    string.Empty);
                string workRoot = Path.Combine(installRoot, "temp", "launcher-update");
                Directory.CreateDirectory(workRoot);
                string manifestPath = Path.Combine(workRoot, "apply-manifest.json");
                File.WriteAllText(manifestPath, "{}");
                string outsideStage = TestFileHelper.CreateTempDirectory("sge-update-trust-outside-stage-");

                Assert.Throws<InvalidOperationException>(() =>
                    LauncherUpdateManifestTrustHelper.ValidateOrThrow(
                        manifestPath,
                        installRoot,
                        outsideStage,
                        Path.Combine(installRoot, LauncherUpdateManifestTrustHelper.LauncherMainExecutableFileName),
                        workRoot,
                        workRoot));
            }
            finally
            {
                try { Directory.Delete(installRoot, recursive: true); } catch { }
            }
        }
    }
}
