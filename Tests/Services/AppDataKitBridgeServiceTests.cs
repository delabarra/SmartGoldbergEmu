using AppDataKit;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SteamKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class AppDataKitBridgeServiceTests
    {
        [Fact]
        public void ConvertToSteamKit_preserves_tree_for_PopulateMetadataFromAppRoot()
        {
            var common = new AppInfoKeyValue("common");
            common.Children.Add(new AppInfoKeyValue("name", "Spacewar"));
            common.Children.Add(new AppInfoKeyValue("type", "Game"));

            var config = new AppInfoKeyValue("config");
            config.Children.Add(new AppInfoKeyValue("installdir", "Spacewar"));

            var root = new AppInfoKeyValue("appinfo");
            root.Children.Add(common);
            root.Children.Add(config);

            KeyValue converted = AppDataKitBridgeService.ConvertToSteamKit(root);
            Assert.NotNull(converted);

            var metadata = new OnlineAppData { AppId = "480" };
            SteamPicsKeyValueHelper.PopulateMetadataFromAppRoot(converted, metadata);

            Assert.Equal("Spacewar", metadata.Name);
            Assert.Equal("Game", metadata.Type);
            Assert.Equal("Spacewar", metadata.InstallDir);
        }
    }
}
