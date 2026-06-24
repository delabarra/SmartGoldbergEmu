using System;

namespace SteamKit
{
internal static class Crc32Util
{
    private static readonly uint[] Table = CreateTable();

    public static byte[] Hash(byte[] data)
    {
        uint crc = 0xFFFFFFFF;

        foreach (byte b in data)
        {
            uint index = (crc ^ b) & 0xFF;
            crc = (crc >> 8) ^ Table[index];
        }

        crc ^= 0xFFFFFFFF;
        return BitConverter.GetBytes(crc);
    }

    private static uint[] CreateTable()
    {
        const uint poly = 0xEDB88320;
        var table = new uint[256];

        for (uint i = 0; i < table.Length; i++)
        {
            uint c = i;
            for (int j = 0; j < 8; j++)
            {
                c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1;
            }

            table[i] = c;
        }

        return table;
    }
}
}
