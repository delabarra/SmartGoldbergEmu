using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;
using SmartGoldbergEmu.Tests.TestSupport;
using SmartGoldbergEmu.Validation;
using Xunit;

namespace SmartGoldbergEmu.Tests.Validation
{
    public sealed class SteamApiResolveTests : IDisposable
    {
        private readonly string _root;
        private readonly List<(string fileKey, string hash)> _registeredHashes = new List<(string, string)>();

        public SteamApiResolveTests()
        {
            _root = TestFileHelper.CreateTempDirectory("sge-api-resolve-");
        }

        public void Dispose()
        {
            foreach ((string fileKey, string hash) in _registeredHashes)
            {
                if (SteamApiHashes.HashMap.TryGetValue(fileKey, out HashSet<string> set))
                    set.Remove(hash);
            }

            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, recursive: true);
            }
            catch
            {
            }
        }

        [Fact]
        public void TryResolveSteamApiForExecutable_prefers_exe_directory_over_compat_and_other_arch()
        {
            string rootApi = Path.Combine(_root, SteamApiValidator.SteamApiDll32);
            string x64Api = Path.Combine(_root, "x64", SteamApiValidator.SteamApiDll64);
            string compatApi = Path.Combine(_root, "compat", SteamApiValidator.SteamApiDll64);
            Directory.CreateDirectory(Path.GetDirectoryName(x64Api));
            Directory.CreateDirectory(Path.GetDirectoryName(compatApi));
            File.WriteAllBytes(rootApi, Encoding.ASCII.GetBytes("root-api"));
            File.WriteAllBytes(x64Api, Encoding.ASCII.GetBytes("x64-api"));
            File.WriteAllBytes(compatApi, Encoding.ASCII.GetBytes("compat-api"));

            string rootExe = Path.Combine(_root, "GrimDawn.exe");
            string x64Exe = Path.Combine(_root, "x64", "GrimDawn.exe");
            File.WriteAllBytes(rootExe, new byte[] { 0x4D, 0x5A });
            File.WriteAllBytes(x64Exe, new byte[] { 0x4D, 0x5A });

            Assert.True(SteamApiValidator.TryResolveSteamApiForExecutable(
                _root, rootExe, useX64: false, out string resolvedRoot));
            Assert.Equal(rootApi, resolvedRoot, StringComparer.OrdinalIgnoreCase);

            Assert.True(SteamApiValidator.TryResolveSteamApiForExecutable(
                _root, x64Exe, useX64: true, out string resolvedX64));
            Assert.Equal(x64Api, resolvedX64, StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual(compatApi, resolvedX64, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetSameHashDeployTargetPaths_excludes_divergent_sibling_hash()
        {
            string preferred = Path.Combine(_root, "x64", SteamApiValidator.SteamApiDll64);
            string compat = Path.Combine(_root, "compat", SteamApiValidator.SteamApiDll64);
            Directory.CreateDirectory(Path.GetDirectoryName(preferred));
            Directory.CreateDirectory(Path.GetDirectoryName(compat));

            byte[] preferredBytes = Encoding.ASCII.GetBytes("preferred-valve-api-bytes-v1");
            byte[] compatBytes = Encoding.ASCII.GetBytes("compat-valve-api-bytes-other");
            File.WriteAllBytes(preferred, preferredBytes);
            File.WriteAllBytes(compat, compatBytes);
            RegisterHash(SteamApiValidator.SteamApiDll64, preferredBytes);
            RegisterHash(SteamApiValidator.SteamApiDll64, compatBytes);

            List<string> targets = SteamApiValidator.GetSameHashDeployTargetPaths(_root, useX64: true, preferred);
            Assert.Single(targets);
            Assert.Equal(Path.GetFullPath(preferred), Path.GetFullPath(targets[0]), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetSameHashDeployTargetPaths_includes_same_hash_peers()
        {
            string preferred = Path.Combine(_root, "bin", SteamApiValidator.SteamApiDll64);
            string peer = Path.Combine(_root, "game", SteamApiValidator.SteamApiDll64);
            Directory.CreateDirectory(Path.GetDirectoryName(preferred));
            Directory.CreateDirectory(Path.GetDirectoryName(peer));

            byte[] sameBytes = Encoding.ASCII.GetBytes("identical-valve-api-payload");
            File.WriteAllBytes(preferred, sameBytes);
            File.WriteAllBytes(peer, sameBytes);
            RegisterHash(SteamApiValidator.SteamApiDll64, sameBytes);

            List<string> targets = SteamApiValidator.GetSameHashDeployTargetPaths(_root, useX64: true, preferred);
            Assert.Equal(2, targets.Count);
            Assert.Contains(targets, p => PathsEqual(p, preferred));
            Assert.Contains(targets, p => PathsEqual(p, peer));
        }

        [Fact]
        public void SteamInterfacesService_writes_only_preferred_dll_interfaces()
        {
            string rootApi = Path.Combine(_root, SteamApiValidator.SteamApiDll32);
            string x64Api = Path.Combine(_root, "x64", SteamApiValidator.SteamApiDll64);
            Directory.CreateDirectory(Path.GetDirectoryName(x64Api));

            byte[] rootBytes = BuildFakeSteamApiBytes("SteamUtils010", "SteamUser017");
            byte[] x64Bytes = BuildFakeSteamApiBytes("SteamUtils011", "SteamUser023");
            File.WriteAllBytes(rootApi, rootBytes);
            File.WriteAllBytes(x64Api, x64Bytes);
            RegisterHash(SteamApiValidator.SteamApiDll32, rootBytes);
            RegisterHash(SteamApiValidator.SteamApiDll64, x64Bytes);

            string exe = CopySystemCmdAsExe(Path.Combine(_root, "game.exe"));
            // Force x86 resolve path regardless of cmd.exe arch: resolve interfaces source with useX64 false via validator.
            Assert.True(SteamApiValidator.TryResolveSteamApiSourceForInterfaces(
                _root, exe, useX64: false, out string source));
            Assert.True(PathsEqual(source, rootApi));

            string settings = Path.Combine(_root, "steam_settings");
            var service = new SteamInterfacesService(new NullLogService());
            Assert.True(service.TryWriteSteamInterfacesFromSteamApi(settings, source, overwrite: true));

            string[] lines = File.ReadAllLines(Path.Combine(settings, PathConstants.GoldbergSteamInterfacesFileName));
            Assert.Contains("SteamUtils010", lines);
            Assert.DoesNotContain("SteamUtils011", lines);
        }

        [Fact]
        public void CollectSteamApiDllPathsForGame_returns_single_path_beside_stored_exe()
        {
            string rootApi = Path.Combine(_root, SteamApiValidator.SteamApiDll32);
            string x64Api = Path.Combine(_root, "x64", SteamApiValidator.SteamApiDll64);
            Directory.CreateDirectory(Path.GetDirectoryName(x64Api));
            byte[] rootBytes = BuildFakeSteamApiBytes("SteamUtils010");
            byte[] x64Bytes = BuildFakeSteamApiBytes("SteamUtils011");
            File.WriteAllBytes(rootApi, rootBytes);
            File.WriteAllBytes(x64Api, x64Bytes);
            RegisterHash(SteamApiValidator.SteamApiDll32, rootBytes);
            RegisterHash(SteamApiValidator.SteamApiDll64, x64Bytes);

            // Use a 32-bit PE so Path-adjacent resolve picks root steam_api.dll.
            string exe32 = WriteMinimalPe(_root, "legacy.exe", isX64: false);
            var game = new GameConfig
            {
                AppId = 219990,
                StartFolder = _root,
                Path = exe32
            };

            var service = new SteamInterfacesService(new NullLogService());
            IReadOnlyList<string> paths = service.CollectSteamApiDllPathsForGame(game);
            Assert.Single(paths);
            Assert.True(PathsEqual(paths[0], rootApi));

            IReadOnlyList<string> names = service.ExtractInterfaceNamesFromGame(game);
            Assert.Contains("SteamUtils010", names);
            Assert.DoesNotContain("SteamUtils011", names);
        }

        [Fact]
        public void TryDetectExecutableIsX64_reads_pe_machine()
        {
            string x86 = WriteMinimalPe(_root, "x86.exe", isX64: false);
            string x64 = WriteMinimalPe(_root, "x64.exe", isX64: true);

            Assert.True(SteamApiValidator.TryDetectExecutableIsX64(x86, out bool isX86));
            Assert.False(isX86);
            Assert.True(SteamApiValidator.TryDetectExecutableIsX64(x64, out bool isX64));
            Assert.True(isX64);
        }

        private void RegisterHash(string fileKey, byte[] content)
        {
            string hash = Sha256Hex(content);
            if (!SteamApiHashes.HashMap.TryGetValue(fileKey, out HashSet<string> set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                SteamApiHashes.HashMap[fileKey] = set;
            }

            set.Add(hash);
            _registeredHashes.Add((fileKey, hash));
        }

        private static byte[] BuildFakeSteamApiBytes(params string[] interfaceNames)
        {
            var sb = new StringBuilder();
            sb.Append("FAKE_STEAM_API_PAYLOAD;");
            foreach (string name in interfaceNames)
                sb.Append(name).Append('\0');
            return Encoding.ASCII.GetBytes(sb.ToString());
        }

        private static string Sha256Hex(byte[] content)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(content);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static bool PathsEqual(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string CopySystemCmdAsExe(string destPath)
        {
            string systemCmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            File.Copy(systemCmd, destPath, overwrite: true);
            return destPath;
        }

        private static string WriteMinimalPe(string directory, string fileName, bool isX64)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            // Minimal PE: MZ + e_lfanew @0x3C + PE signature + Machine
            var data = new byte[0x80];
            data[0] = 0x4D; // M
            data[1] = 0x5A; // Z
            const int peOffset = 0x40;
            BitConverter.GetBytes(peOffset).CopyTo(data, 0x3C);
            data[peOffset] = 0x50; // P
            data[peOffset + 1] = 0x45; // E
            data[peOffset + 2] = 0x00;
            data[peOffset + 3] = 0x00;
            ushort machine = isX64 ? (ushort)0x8664 : (ushort)0x14C;
            BitConverter.GetBytes(machine).CopyTo(data, peOffset + 4);
            File.WriteAllBytes(path, data);
            return path;
        }
    }
}
