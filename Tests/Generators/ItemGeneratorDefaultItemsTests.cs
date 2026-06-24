using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.JsonKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.Generators
{
    public sealed class ItemGeneratorDefaultItemsTests
    {
        [Fact]
        public void BuildDefaultItemsMap_matches_goldberg_example_shape()
        {
            var items = new JsonObject();
            items["2001"] = new JsonObject { ["name"] = new JsonString("Foster Classic Bundle") };
            items["2002"] = new JsonObject { ["name"] = new JsonString("Briar's Bobby Bundle") };

            JsonObject defaults = ItemGenerator.BuildDefaultItemsMap(items);

            Assert.Equal(2, defaults.Count);
            var slot1 = defaults["1"] as JsonObject;
            var slot2 = defaults["2"] as JsonObject;
            Assert.NotNull(slot1);
            Assert.NotNull(slot2);
            Assert.Equal(2001, ((JsonNumber)slot1["definition"]).IntegerValue);
            Assert.Equal(1, ((JsonNumber)slot1["quantity"]).IntegerValue);
            Assert.Equal(2002, ((JsonNumber)slot2["definition"]).IntegerValue);
            Assert.Equal(1, ((JsonNumber)slot2["quantity"]).IntegerValue);
        }

        [Fact]
        public void BuildDefaultItemsMap_uses_count_field_when_present()
        {
            var items = new JsonObject();
            var pack = new JsonObject();
            pack["count"] = new JsonString("6");
            items["101"] = pack;

            JsonObject defaults = ItemGenerator.BuildDefaultItemsMap(items);

            Assert.Single(defaults.Properties());
            var slot = defaults["1"] as JsonObject;
            Assert.Equal(101, ((JsonNumber)slot["definition"]).IntegerValue);
            Assert.Equal(6, ((JsonNumber)slot["quantity"]).IntegerValue);
        }
    }
}
