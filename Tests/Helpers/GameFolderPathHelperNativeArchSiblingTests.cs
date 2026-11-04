using System;
using System.IO;
using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class GameFolderPathHelperNativeArchSiblingTests
    {
        [Fact]
        public void TryResolveSameNamedNativeArchSibling_prefers_x64_folder_sibling_on_64bit_os()
        {
            if (!Environment.Is64BitOperatingSystem)
                return;

            string root = Path.Combine(Path.GetTempPath(), "sge_arch_sib_" + Guid.NewGuid().ToString("N"));
            string x64Dir = Path.Combine(root, "x64");
            Directory.CreateDirectory(x64Dir);
            string rootExe = Path.Combine(root, "Game.exe");
            string x64Exe = Path.Combine(x64Dir, "Game.exe");
            WriteMinimalPe(rootExe, machine: 0x014C);
            WriteMinimalPe(x64Exe, machine: 0x8664);

            try
            {
                Assert.True(GameFolderPathHelper.TryResolveSameNamedNativeArchSibling(rootExe, out string native));
                Assert.Equal(Path.GetFullPath(x64Exe), Path.GetFullPath(native));
                Assert.False(GameFolderPathHelper.TryResolveSameNamedNativeArchSibling(x64Exe, out _));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }

        private static void WriteMinimalPe(string path, ushort machine)
        {
            // MZ + PE offset at 0x3C + PE signature + Machine — enough for TryDetectExecutableIsX64.
            var bytes = new byte[0x80];
            bytes[0] = 0x4D;
            bytes[1] = 0x5A;
            bytes[0x3C] = 0x40;
            bytes[0x40] = 0x50;
            bytes[0x41] = 0x45;
            bytes[0x42] = 0x00;
            bytes[0x43] = 0x00;
            bytes[0x44] = (byte)(machine & 0xFF);
            bytes[0x45] = (byte)((machine >> 8) & 0xFF);
            File.WriteAllBytes(path, bytes);
        }
    }
}
