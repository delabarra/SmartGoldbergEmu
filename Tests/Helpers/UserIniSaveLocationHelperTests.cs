using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public class UserIniSaveLocationHelperTests
    {
        [Fact]
        public void ApplySaveLocationToIni_omits_keys_for_default_global_saves()
        {
            var iniService = new IniFileService();
            var iniFile = iniService.ParseFile(Path.GetTempFileName());
            var settings = new UserSettings
            {
                LocalSavePath = string.Empty,
                SavesFolderName = ApplicationConstants.DefaultSavesFolderName,
            };

            UserIniSaveLocationHelper.ApplySaveLocationToIni(iniService, iniFile, settings);

            Assert.True(string.IsNullOrEmpty(iniService.GetValue(iniFile, UserIniSaveLocationHelper.SavesSection, UserIniSaveLocationHelper.LocalSavePathKey)));
            Assert.True(string.IsNullOrEmpty(iniService.GetValue(iniFile, UserIniSaveLocationHelper.SavesSection, UserIniSaveLocationHelper.SavesFolderNameKey)));
        }

        [Fact]
        public void ApplySaveLocationToIni_writes_portable_local_save_path()
        {
            var iniService = new IniFileService();
            var iniFile = iniService.ParseFile(Path.GetTempFileName());
            var settings = new UserSettings
            {
                LocalSavePath = "./",
                SavesFolderName = "My Saves",
            };

            UserIniSaveLocationHelper.ApplySaveLocationToIni(iniService, iniFile, settings);

            Assert.Equal("./My Saves", iniService.GetValue(iniFile, UserIniSaveLocationHelper.SavesSection, UserIniSaveLocationHelper.LocalSavePathKey));
            Assert.True(string.IsNullOrEmpty(iniService.GetValue(iniFile, UserIniSaveLocationHelper.SavesSection, UserIniSaveLocationHelper.SavesFolderNameKey)));
        }

        [Fact]
        public void ExpandPortableLocalSavePathForIni_uses_saves_folder_under_dll_root()
        {
            string path = UserIniSaveLocationHelper.ExpandPortableLocalSavePathForIni(
                "./",
                new UserSettings { SavesFolderName = ApplicationConstants.DefaultSavesFolderName });

            Assert.Equal("./" + ApplicationConstants.DefaultSavesFolderName, path);
        }

        [Fact]
        public void ResolveGlobalSaveFields_clears_saves_folder_for_steam_userdata()
        {
            UserIniSaveLocationHelper.ResolveGlobalSaveFields(
                out string localSavePath,
                out string savesFolderName,
                @"C:\Steam\userdata\12345",
                "CustomName",
                isSteamUserdataMode: true,
                accountSteamId: string.Empty);

            Assert.Equal(string.Empty, savesFolderName);
            Assert.Contains("userdata", localSavePath);
        }
    }
}
