using System.IO;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class LauncherUpdatePayloadHelperTests
    {
        [Fact]
        public void ResolvePayloadRoot_uses_nested_version_folder_when_exe_not_at_root()
        {
            string extractRoot = TestFileHelper.CreateTempDirectory("sge-update-extract-");
            string nested = Path.Combine(extractRoot, "SmartGoldbergEmu-2.4.0");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "SmartGoldbergEmu.exe"), string.Empty);

            string payload = LauncherUpdatePayloadHelper.ResolvePayloadRoot(extractRoot, "SmartGoldbergEmu.exe");

            Assert.Equal(nested, payload);
        }

        [Fact]
        public void ResolvePayloadRoot_keeps_flat_layout()
        {
            string extractRoot = TestFileHelper.CreateTempDirectory("sge-update-flat-");
            File.WriteAllText(Path.Combine(extractRoot, "SmartGoldbergEmu.exe"), string.Empty);

            string payload = LauncherUpdatePayloadHelper.ResolvePayloadRoot(extractRoot, "SmartGoldbergEmu.exe");

            Assert.Equal(extractRoot, payload);
        }
    }
}
