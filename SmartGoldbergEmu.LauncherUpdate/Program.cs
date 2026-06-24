using System;

namespace SmartGoldbergEmu.LauncherUpdate
{
    internal static class Program
    {
        private const string ManifestFlag = "--manifest";

        static int Main(string[] args)
        {
            string manifestPath = TryParseManifestPath(args);
            if (string.IsNullOrEmpty(manifestPath))
                return 1;

            try
            {
                LauncherUpdateApplyRunner.Run(manifestPath);
                return 0;
            }
            catch
            {
                return 1;
            }
        }

        private static string TryParseManifestPath(string[] args)
        {
            if (args == null || args.Length == 0)
                return null;

            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], ManifestFlag, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (i + 1 >= args.Length)
                    return null;

                return args[i + 1].Trim().Trim('"');
            }

            return null;
        }
    }
}
