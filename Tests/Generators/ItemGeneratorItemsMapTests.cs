using System.Globalization;
using System.Threading;
using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.JsonKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.Generators
{
    public sealed class ItemGeneratorItemsMapTests
    {
        [Fact]
        public void TryBuildItemDefinitionMap_converts_scalar_attributes_to_strings()
        {
            const string archive = "[{\"itemdefid\":101,\"appid\":480,\"name\":\"Hat\",\"tradable\":true,\"marketable\":false,\"tags\":[\"a\"]}]";

            Assert.True(ItemGenerator.TryBuildItemDefinitionMap(archive, out JsonObject map, out string error), error);

            var item = Assert.IsType<JsonObject>(map["101"]);
            Assert.Equal("101", ((JsonString)item["itemdefid"]).Value);
            Assert.Equal("480", ((JsonString)item["appid"]).Value);
            Assert.Equal("Hat", ((JsonString)item["name"]).Value);
            Assert.Equal("true", ((JsonString)item["tradable"]).Value);
            Assert.Equal("false", ((JsonString)item["marketable"]).Value);
            Assert.IsType<JsonArray>(item["tags"]);
        }

        [Fact]
        public void TryBuildItemDefinitionMap_skips_array_entries_without_numeric_itemdefid()
        {
            const string archive = "[{\"name\":\"No id\"},{\"itemdefid\":\"abc\",\"name\":\"Bad id\"},{\"itemdefid\":7,\"name\":\"Ok\"}]";

            Assert.True(ItemGenerator.TryBuildItemDefinitionMap(archive, out JsonObject map, out string error), error);

            Assert.Equal(1, map.Count);
            Assert.NotNull(map["7"]);
        }

        [Fact]
        public void TryBuildItemDefinitionMap_fails_when_no_entry_has_numeric_itemdefid()
        {
            const string archive = "[{\"name\":\"No id\"}]";

            Assert.False(ItemGenerator.TryBuildItemDefinitionMap(archive, out _, out string error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void TryBuildItemDefinitionMap_keyed_archive_skips_non_numeric_keys_and_uses_invariant_numbers()
        {
            const string archive = "{\"20\":{\"name\":\"Ok\",\"price\":1.5},\"item_1\":{\"name\":\"Bad key\"}}";
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

                Assert.True(ItemGenerator.TryBuildItemDefinitionMap(archive, out JsonObject map, out string error), error);

                Assert.Equal(1, map.Count);
                var item = Assert.IsType<JsonObject>(map["20"]);
                Assert.Equal("1.5", ((JsonString)item["price"]).Value);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
