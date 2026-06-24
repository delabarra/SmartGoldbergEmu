using System.IO;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class IniFileServiceTests
    {
        [Fact]
        public void ParseFile_and_WriteFile_round_trip_key_value()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-ini-");
            try
            {
                string path = Path.Combine(dir, "sample.ini");
                File.WriteAllText(path,
                    "[main]\r\n" +
                    "language=english\r\n" +
                    "; comment\r\n" +
                    "\r\n" +
                    "[overlay]\r\n" +
                    "enable=1\r\n");

                var service = new IniFileService();
                var ini = service.ParseFile(path);
                Assert.Equal("english", service.GetValue(ini, "main", "language"));
                Assert.Equal("1", service.GetValue(ini, "overlay", "enable"));

                service.SetValue(ini, "main", "language", "french");
                service.WriteFile(ini, path);

                var reloaded = service.ParseFile(path);
                Assert.Equal("french", service.GetValue(reloaded, "main", "language"));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void SetValue_appends_new_section_when_missing()
        {
            var service = new IniFileService();
            var ini = service.ParseFile(Path.Combine(TestFileHelper.CreateTempDirectory("sge-ini-"), "missing.ini"));
            service.SetValue(ini, "user", "name", "duke");
            Assert.Equal("duke", service.GetValue(ini, "user", "name"));
        }
    }
}
