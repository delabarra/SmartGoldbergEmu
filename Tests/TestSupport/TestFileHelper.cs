using System;
using System.IO;
using System.Reflection;

namespace SmartGoldbergEmu.Tests.TestSupport
{
    internal static class TestFileHelper
    {
        public static string GetTestDataPath(string relativePath)
        {
            string fileName = relativePath.Replace('/', Path.DirectorySeparatorChar);
            foreach (string root in GetTestDataSearchRoots())
            {
                string candidate = Path.Combine(root, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            throw new FileNotFoundException(
                "Test data file not found. Copy TestData to the test output directory.",
                fileName);
        }

        private static string[] GetTestDataSearchRoots()
        {
            var roots = new System.Collections.Generic.List<string>();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
                roots.Add(Path.Combine(baseDir, "TestData"));

            try
            {
                string assemblyPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
                string dir = Path.GetDirectoryName(assemblyPath);
                for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
                {
                    string candidate = Path.Combine(dir, "TestData");
                    if (Directory.Exists(candidate) && !roots.Contains(candidate))
                        roots.Add(candidate);
                    dir = Directory.GetParent(dir)?.FullName;
                }
            }
            catch
            {
            }

            return roots.ToArray();
        }

        public static string ReadTestData(string relativePath) =>
            File.ReadAllText(GetTestDataPath(relativePath));

        public static string NormalizeNewlines(string text)
        {
            if (text == null)
                return string.Empty;
            return text.Replace("\r\n", "\n").TrimEnd();
        }

        public static string CreateTempDirectory(string prefix)
        {
            string path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
