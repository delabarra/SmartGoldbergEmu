using System.Collections.Generic;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class DlcServiceParseTests
    {
        [Fact]
        public void ParseDlcListText_returns_null_for_empty_text()
        {
            Assert.Null(DlcService.ParseDlcListText(null));
            Assert.Null(DlcService.ParseDlcListText(string.Empty));
            Assert.Null(DlcService.ParseDlcListText("   \r\n  "));
        }

        [Fact]
        public void ParseDlcListText_parses_id_name_lines_and_id_only_lines()
        {
            var parsed = DlcService.ParseDlcListText("480 - Spacewar\r\n999\r\nnot-a-number");

            Assert.NotNull(parsed);
            Assert.Equal(2, parsed.Count);
            Assert.Equal("Spacewar", parsed[480]);
            Assert.Equal("DLC 999", parsed[999]);
        }

        [Fact]
        public void BuildDlcListText_round_trips_sorted_rows()
        {
            var data = new Dictionary<long, string>
            {
                { 200, "Beta DLC" },
                { 100, "Alpha DLC" }
            };

            string text = DlcService.BuildDlcListText(data);
            var parsed = DlcService.ParseDlcListText(text);

            Assert.NotNull(parsed);
            Assert.Equal(2, parsed.Count);
            Assert.Equal("Alpha DLC", parsed[100]);
            Assert.Equal("Beta DLC", parsed[200]);
        }

        [Fact]
        public void BuildDlcListTextWithPreferredNames_uses_preferred_name_when_present()
        {
            var data = new Dictionary<long, string> { { 100, "Old Name" } };
            var preferred = new Dictionary<long, string> { { 100, "Preferred Name" } };

            string text = DlcService.BuildDlcListTextWithPreferredNames(data, preferred);

            Assert.Contains("100 - Preferred Name", text);
            Assert.DoesNotContain("Old Name", text);
        }
    }
}
