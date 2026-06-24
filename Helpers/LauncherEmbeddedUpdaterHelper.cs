using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.JsonKit;

namespace SmartGoldbergEmu.Helpers
{
    public static class LauncherEmbeddedUpdaterHelper
    {
        public const string EmbeddedUpdaterResourceName = "SmartGoldbergEmu.LauncherUpdate.exe";

        public static string GetUpdaterPath(string workRoot)
        {
            return Path.Combine(workRoot, PathConstants.LauncherUpdateEmbeddedUpdaterFileName);
        }

        public static string GetManifestPath(string workRoot)
        {
            return Path.Combine(workRoot, PathConstants.LauncherUpdateApplyManifestFileName);
        }

        public static void ExtractEmbeddedUpdater(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("Destination path is required.", nameof(destinationPath));

            Assembly assembly = typeof(LauncherEmbeddedUpdaterHelper).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(EmbeddedUpdaterResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "Embedded launcher updater is missing from this build. Rebuild the full solution.");
                }

                string directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream fileStream = File.Create(destinationPath))
                {
                    stream.CopyTo(fileStream);
                }
            }
        }

        public static void WriteApplyManifest(
            string manifestPath,
            string installRoot,
            string stageRoot,
            string exePath,
            string workRoot,
            int waitProcessId,
            string[] skipDirectoryNames)
        {
            var manifest = new JsonObject
            {
                ["installRoot"] = new JsonString(installRoot ?? string.Empty),
                ["stageRoot"] = new JsonString(stageRoot ?? string.Empty),
                ["exePath"] = new JsonString(exePath ?? string.Empty),
                ["workRoot"] = new JsonString(workRoot ?? string.Empty),
                ["waitProcessId"] = new JsonNumber(waitProcessId)
            };

            var skipArray = new JsonArray();
            if (skipDirectoryNames != null)
            {
                foreach (string skipName in skipDirectoryNames)
                    skipArray.Add(new JsonString(skipName ?? string.Empty));
            }

            manifest["skipDirectoryNames"] = skipArray;
            File.WriteAllText(manifestPath, manifest.ToJsonString());
        }

        public static void StartEmbeddedUpdater(string updaterPath, string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(updaterPath) || !File.Exists(updaterPath))
                throw new FileNotFoundException("Launcher updater executable was not extracted.", updaterPath);

            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                throw new FileNotFoundException("Launcher update manifest was not written.", manifestPath);

            string workRoot = Path.GetDirectoryName(manifestPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = "--manifest \"" + manifestPath + "\"",
                WorkingDirectory = string.IsNullOrEmpty(workRoot) ? PathConstants.AppBaseDirectory : workRoot,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo);
        }
    }
}
