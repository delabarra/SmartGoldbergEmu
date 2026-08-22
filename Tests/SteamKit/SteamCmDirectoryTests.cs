using System.Collections.Generic;
using SteamKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.SteamKit
{
    public sealed class SteamCmDirectoryTests
    {
        [Fact]
        public void ParseServerList_keeps_tcp_ip_strings_from_GetCMList()
        {
            const string json =
                "{\"response\":{\"serverlist\":[\"162.254.192.101:27017\",\"185.25.182.52:27017\"]," +
                "\"serverlist_websockets\":[\"cmp1-fra1.steamserver.net:443\"]}}";

            List<string> endpoints = SteamCmDirectory.ParseServerListFromDirectoryResponse(json);

            Assert.Equal(new[] { "162.254.192.101:27017", "185.25.182.52:27017" }, endpoints);
        }

        [Fact]
        public void ParseServerList_keeps_only_netfilter_tcp_from_GetCMListForConnect_in_steam_order()
        {
            const string json =
                "{\"response\":{\"serverlist\":[" +
                "{\"endpoint\":\"cmp1-fra2.steamserver.net:27023\",\"legacy_endpoint\":\"cmp1-fra2.steamserver.net:27023\",\"type\":\"websockets\"}," +
                "{\"endpoint\":\"155.133.226.74:27017\",\"legacy_endpoint\":\"155.133.226.74:27017\",\"type\":\"netfilter\"}," +
                "{\"endpoint\":\"cmp1-fra1.steamserver.net:443\",\"legacy_endpoint\":\"cmp1-fra1.steamserver.net:443\",\"type\":\"websockets\"}," +
                "{\"endpoint\":\"155.133.252.54:27017\",\"legacy_endpoint\":\"155.133.252.54:27017\",\"type\":\"netfilter\"}" +
                "]}}";

            List<string> endpoints = SteamCmDirectory.ParseServerListFromDirectoryResponse(json);

            Assert.Equal(new[] { "155.133.226.74:27017", "155.133.252.54:27017" }, endpoints);
        }

        [Fact]
        public void ParseServerList_ignores_quoted_type_and_dc_strings()
        {
            const string json =
                "{\"response\":{\"serverlist\":[" +
                "{\"endpoint\":\"cmp2-iad1.steamserver.net:443\",\"type\":\"websockets\",\"dc\":\"iad1\",\"realm\":\"steamglobal\"}" +
                "]}}";

            List<string> endpoints = SteamCmDirectory.ParseServerListFromDirectoryResponse(json);

            Assert.Empty(endpoints);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("websockets", false)]
        [InlineData("fra1", false)]
        [InlineData("cmp1-fra1.steamserver.net:443", false)]
        [InlineData("155.133.226.74:27017", true)]
        [InlineData("185.25.182.52:27017", true)]
        public void IsUsableTcpCmEndpoint_rejects_websocket_and_non_endpoints(string endpoint, bool expected)
        {
            Assert.Equal(expected, SteamCmDirectory.IsUsableTcpCmEndpoint(endpoint));
        }
    }
}
