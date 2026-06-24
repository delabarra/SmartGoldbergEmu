using System.Collections.Generic;
using System.IO;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Tests.TestSupport
{
    public static class GoldbergTestLayoutHelper
    {
        public static void WriteEmptyFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[] { 0 });
        }

        public static void StageSteamClientGoldbergFiles()
        {
            WriteEmptyFile(PathConstants.CombineGoldbergSteamClientDllPath(false));
            WriteEmptyFile(PathConstants.CombineGoldbergSteamClientDllPath(true));
            WriteEmptyFile(PathConstants.CombineGoldbergGameOverlayRendererPath(false));
            WriteEmptyFile(PathConstants.CombineGoldbergGameOverlayRendererPath(true));
        }

        public static void StageGoldbergExperimentalFiles(bool useX64)
        {
            WriteEmptyFile(PathConstants.CombineGoldbergExperimentalSteamApiPath(useX64));
            WriteEmptyFile(PathConstants.CombineGoldbergExperimentalSteamClientPath(useX64));
        }

        public static void StageSteamDllInGoldbergFolder()
        {
            WriteEmptyFile(PathConstants.CombineGoldbergSteamDllPath());
        }

        public static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        public static void CleanupStagedFiles(IEnumerable<string> paths)
        {
            foreach (string path in paths)
                DeleteIfExists(path);
        }
    }
}
