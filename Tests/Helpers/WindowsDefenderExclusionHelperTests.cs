using System;
using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class WindowsDefenderExclusionHelperTests
    {
        [Fact]
        public void BuildMpPreferenceScript_batches_three_paths_for_add()
        {
            string script = WindowsDefenderExclusionHelper.BuildMpPreferenceScript(
                new[]
                {
                    @"C:\temp\coldclient-download.7z",
                    @"C:\app\goldberg\coldclient\steamclient_loader_x86.exe",
                    @"C:\app\goldberg\coldclient\steamclient_loader_x64.exe",
                },
                add: true);

            Assert.Contains("Add-MpPreference -ExclusionPath 'C:\\temp\\coldclient-download.7z'", script);
            Assert.Contains("Add-MpPreference -ExclusionPath 'C:\\app\\goldberg\\coldclient\\steamclient_loader_x86.exe'", script);
            Assert.Contains("Add-MpPreference -ExclusionPath 'C:\\app\\goldberg\\coldclient\\steamclient_loader_x64.exe'", script);
            Assert.Contains("; ", script);
            Assert.DoesNotContain("Remove-MpPreference", script);
        }

        [Fact]
        public void BuildMpPreferenceScript_escapes_single_quotes()
        {
            string script = WindowsDefenderExclusionHelper.BuildMpPreferenceScript(
                new[] { @"C:\O'Brien\file.7z" },
                add: false);
            Assert.Equal("Remove-MpPreference -ExclusionPath 'C:\\O''Brien\\file.7z'", script);
        }

        [Fact]
        public void SanitizeExclusionPath_rejects_metacharacters()
        {
            Assert.Throws<InvalidOperationException>(() =>
                WindowsDefenderExclusionHelper.SanitizeExclusionPath(@"C:\temp\a|b.7z"));
        }
    }
}
