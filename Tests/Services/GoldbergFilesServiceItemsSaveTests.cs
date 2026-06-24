using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class GoldbergFilesServiceItemsSaveTests
    {
        [Fact]
        public void SaveAdditionalFiles_add_mode_skips_empty_items_json_and_preserves_generated_file()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-items-");
            try
            {
                const ulong appId = 588430;
                var service = new GoldbergFilesService(gamesRoot);
                string settingsPath = Path.Combine(gamesRoot, appId.ToString(), PathConstants.SteamSettingsFolderName);
                Directory.CreateDirectory(settingsPath);
                string itemsPath = Path.Combine(settingsPath, PathConstants.GoldbergItemsJsonFileName);
                const string generated = "{\"100\":{\"name\":\"Vault-Tec Starter Pack\"}}";
                File.WriteAllText(itemsPath, generated);

                var failures = service.SaveAdditionalFiles(new GoldbergFilesService.AdditionalFilesSaveRequest
                {
                    AppId = appId,
                    IsEditMode = false,
                    ItemsJson = "{}"
                });

                Assert.Empty(failures);
                Assert.Equal(generated, File.ReadAllText(itemsPath));
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }
    }
}
