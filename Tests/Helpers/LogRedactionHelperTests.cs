using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class LogRedactionHelperTests
    {
        [Fact]
        public void RedactForLog_redacts_steam_id64()
        {
            const string steamId = "76561198000000000";
            string result = LogRedactionHelper.RedactForLog("account " + steamId);
            Assert.DoesNotContain(steamId, result);
            Assert.Contains("<steamid>", result);
        }

        [Fact]
        public void RedactForLog_redacts_api_key_query_parameter()
        {
            string result = LogRedactionHelper.RedactForLog("https://api.example.com/?key=ABCDEF0123456789");
            Assert.DoesNotContain("ABCDEF0123456789", result);
            Assert.Contains("key=***", result);
        }

        [Fact]
        public void RedactApiKey_redacts_literal_key_value()
        {
            const string key = "0123456789ABCDEF0123456789ABCDEF";
            string result = LogRedactionHelper.RedactApiKey("failed with key " + key, key);
            Assert.DoesNotContain(key, result);
            Assert.Contains("***", result);
        }

        [Fact]
        public void RedactForLog_redacts_windows_drive_path()
        {
            string result = LogRedactionHelper.RedactForLog(@"failed at C:\Games\Foo\game.exe");
            Assert.DoesNotContain(@"C:\Games", result);
            Assert.Contains("<path>", result);
        }
    }
}
