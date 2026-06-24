using System.Collections.Generic;
using SmartGoldbergEmu.JsonKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.JsonKit
{
    public sealed class JsonKitTests
    {
        [Fact]
        public void Parse_reads_object_properties()
        {
            var root = JsonValue.Parse("{\"name\":\"Duke\",\"count\":3}") as JsonObject;
            Assert.NotNull(root);
            Assert.Equal("Duke", root["name"].ToString());
            Assert.Equal(3, ((JsonNumber)root["count"]).IntegerValue);
        }

        [Fact]
        public void DeserializeStringDictionary_round_trips_keys()
        {
            const string json = "{\"beta\":\"public\",\"lang\":\"english\"}";
            Dictionary<string, string> dict = JsonConvert.DeserializeStringDictionary(json);
            Assert.Equal(2, dict.Count);
            Assert.Equal("public", dict["beta"]);
            Assert.Equal("english", dict["lang"]);

            string again = JsonConvert.SerializeObject(dict);
            Dictionary<string, string> roundTrip = JsonConvert.DeserializeStringDictionary(again);
            Assert.Equal(dict["beta"], roundTrip["beta"]);
            Assert.Equal(dict["lang"], roundTrip["lang"]);
        }

        [Fact]
        public void ToJsonString_preserves_nested_array()
        {
            var root = JsonValue.Parse("{\"items\":[1,2]}") as JsonObject;
            string json = root.ToJsonString();
            var reparsed = JsonValue.Parse(json) as JsonObject;
            var items = reparsed["items"] as JsonArray;
            Assert.NotNull(items);
            Assert.Equal(2, items.Count);
        }
    }
}
