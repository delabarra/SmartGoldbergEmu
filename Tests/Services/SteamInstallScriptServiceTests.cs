using System;
using System.IO;
using System.Text;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;
using SteamKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public class SteamInstallScriptServiceTests
    {
        [Fact]
        public void Presence_OpenALDepot_IsConfidentlyPresentMatchesSystemProbe()
        {
            // Depot 229020 → OpenAL; confident present iff OpenAL32/wrap_oal is on the machine.
            bool present = SteamRedistributablePresence.IsConfidentlyPresent(
                229020,
                null,
                @"_CommonRedist\OpenAL\2.0.7.0\installscript.vdf");
            bool unknownPath = SteamRedistributablePresence.IsPresentOrUnknown(
                229020,
                null,
                @"_CommonRedist\OpenAL\2.0.7.0\installscript.vdf");
            Assert.Equal(present, unknownPath);
        }

        [Fact]
        public void Presence_PhysXDepot_IsNotConfidentlyPresentWithoutProbe()
        {
            // PhysX has no DLL probe — must not claim "confidently present" or downloads would be skipped forever.
            Assert.False(SteamRedistributablePresence.IsConfidentlyPresent(
                229033,
                null,
                @"_CommonRedist\PhysX\9.14.0702\installscript.vdf"));
            Assert.True(SteamRedistributablePresence.IsPresentOrUnknown(
                229033,
                null,
                @"_CommonRedist\PhysX\9.14.0702\installscript.vdf"));
        }

        [Fact]
        public void Presence_Vc2005_UsesWinSxSProbeNotSystem32Only()
        {
            // Depot 228981 must resolve to a real probe (WinSxS), not "unknown".
            // ConfidentlyPresent may be true or false depending on the machine; PresentOrUnknown must match it.
            bool present = SteamRedistributablePresence.IsConfidentlyPresent(
                228981,
                null,
                @"_CommonRedist\vcredist\2005\installscript.vdf");
            bool presentOrUnknown = SteamRedistributablePresence.IsPresentOrUnknown(
                228981,
                null,
                @"_CommonRedist\vcredist\2005\installscript.vdf");
            Assert.Equal(present, presentOrUnknown);
        }

        [Fact]
        public void Presence_Vc2022_X86AndX64AreIndependent()
        {
            // Arch-specific checks must not collapse to "either System32 or SysWOW64".
            bool x86 = SteamRedistributablePresence.IsConfidentlyPresent(
                228989,
                null,
                @"_CommonRedist\vcredist\2022\installscript.vdf",
                "x86");
            bool x64 = SteamRedistributablePresence.IsConfidentlyPresent(
                228989,
                null,
                @"_CommonRedist\vcredist\2022\installscript.vdf",
                "x64");
            bool both = SteamRedistributablePresence.IsConfidentlyPresent(
                228989,
                null,
                @"_CommonRedist\vcredist\2022\installscript.vdf");

            // Depot-level presence requires every arch the OS needs.
            if (Environment.Is64BitOperatingSystem)
                Assert.Equal(x86 && x64, both);
            else
                Assert.Equal(x86, both);
        }

        [Fact]
        public void ParseVdf_HotlineStyleInstallScript_HasRunProcessEntry()
        {
            const string vdf =
                "\"InstallScript\"\n" +
                "{\n" +
                "\t\"Run Process\"\n" +
                "\t{\n" +
                "\t\t\"Visual C++ 2010 SP1 Redistributable Package (x86)\"\n" +
                "\t\t{\n" +
                "\t\t\t\"process 1\"\t\t\"%INSTALLDIR%\\\\redist\\\\vcredist_x86.exe\"\n" +
                "\t\t\t\"command 1\"\t\t\"/q:a\"\n" +
                "\t\t\t\"IgnoreExitCode\"\t\t\"1\"\n" +
                "\t\t}\n" +
                "\t}\n" +
                "}\n";

            KeyValue root = KeyValue.ParseVdf(Encoding.UTF8.GetBytes(vdf));
            Assert.NotNull(root);
            Assert.Equal("InstallScript", root.Name);
            Assert.NotNull(root.Children);
            Assert.Contains(root.Children, c => string.Equals(c.Name, "Run Process", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EnsureDependenciesForFolder_MissingFolder_SucceedsSoft()
        {
            var service = new SteamInstallScriptService(new NullLogService(), new SteamProductInfoService());

            var result = service.EnsureDependenciesForFolder(
                1,
                Path.Combine(Path.GetTempPath(), "sge-missing-install-folder-" + Guid.NewGuid().ToString("N")));

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void EnsureDependenciesForFolder_HotlineMiami_WhenInstalled_RunsSoft()
        {
            const string hotlinePath = @"g:\Steam Games\steamapps\common\hotline_miami";
            if (!Directory.Exists(hotlinePath))
                return;

            string script = Path.Combine(hotlinePath, "219150_install.vdf");
            Assert.True(File.Exists(script), "Expected Hotline Miami install script beside the game.");

            var logger = new CapturingLogService();
            var service = new SteamInstallScriptService(logger, new SteamProductInfoService());

            // dry-run: discover game + common-redist scripts without launching installers.
            var result = service.EnsureDependenciesForFolder(219150, hotlinePath, "Hotline Miami", dryRun: true);
            Assert.True(result.IsSuccess);
            Assert.Contains(logger.Messages, m => m.IndexOf("Install scripts", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.Contains(logger.Messages, m =>
                m.IndexOf("pending", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("Visual C++", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private sealed class CapturingLogService : Abstractions.ILogService
        {
            public System.Collections.Generic.List<string> Messages { get; } =
                new System.Collections.Generic.List<string>();

            public void LogMessage(string message) => Messages.Add(message ?? string.Empty);
            public void LogDebug(string message) => Messages.Add(message ?? string.Empty);
            public void LogWarning(string message) => Messages.Add(message ?? string.Empty);
            public void LogError(string message) => Messages.Add(message ?? string.Empty);
            public void LogError(string message, Exception ex) =>
                Messages.Add((message ?? string.Empty) + (ex != null ? " " + ex.Message : string.Empty));
        }
    }
}
