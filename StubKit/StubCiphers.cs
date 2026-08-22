using System;
using System.IO;
using System.Security.Cryptography;

namespace SmartGoldbergEmu.StubKit
{
    // SteamStub wrapper crypto: chained DWORD XOR, AES-CBC with derived IV, XTEA+chain for DRMP.
    internal static class StubCiphers
    {
        // DWORD chain XOR. key==0 => seed is first DWORD and decode starts at offset 4.
        public static uint DecodeChainedDwords(byte[] data, uint size, uint key)
        {
            uint offset = 0;
            if (key == 0)
            {
                offset = 4;
                key = BitConverter.ToUInt32(data, 0);
            }

            for (uint x = offset; x + 4 <= size; x += 4)
            {
                uint val = BitConverter.ToUInt32(data, (int)x);
                WriteU32(data, (int)x, val ^ key);
                key = val;
            }
            return key;
        }

        // V1.0 header: each byte ^= index*index.
        public static void XorByIndexSquared(byte[] data)
        {
            for (int x = 0; x < data.Length; x++)
                data[x] ^= (byte)(x * x);
        }

        // V2.0 code section: rolling XOR over a DWORD run (key updates from ciphertext).
        public static void DecodeChainedDwordRun(byte[] data, uint dwordCount, uint key)
        {
            int offset = 0;
            for (uint x = dwordCount; x > 0; --x)
            {
                uint val1 = BitConverter.ToUInt32(data, offset);
                uint val2 = val1 ^ key;
                key = val1;
                WriteU32(data, offset, val2);
                offset += 4;
            }
        }

        // Turn the stored IV into the CBC IV via AES-ECB decrypt of the IV block.
        public static void DeriveCbcIv(byte[] key, byte[] iv)
        {
            using (var aes = new AesCryptoServiceProvider())
            {
                aes.Key = key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.IV = new byte[16];
                using (var decryptor = aes.CreateDecryptor())
                {
                    var tmp = new byte[16];
                    if (decryptor.TransformBlock(iv, 0, 16, tmp, 0) != 16)
                        throw new InvalidOperationException("IV derive failed.");
                    Buffer.BlockCopy(tmp, 0, iv, 0, 16);
                }
            }
        }

        public static byte[] DecryptAesCbcWithDerivedIv(byte[] key, byte[] iv, byte[] cipher)
        {
            byte[] useIv = (byte[])iv.Clone();
            DeriveCbcIv(key, useIv);

            using (var aes = new AesCryptoServiceProvider())
            {
                aes.Key = key;
                aes.IV = useIv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(cipher))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    var plain = new byte[cipher.Length];
                    int read = 0;
                    while (read < plain.Length)
                    {
                        int n = cs.Read(plain, read, plain.Length - read);
                        if (n <= 0)
                            break;
                        read += n;
                    }
                    if (read != plain.Length)
                        throw new InvalidOperationException("AES decrypt produced incomplete output.");
                    return plain;
                }
            }
        }

        public static byte[] DecryptCodeSection(byte[] stolen16, byte[] encryptedBody, byte[] key, byte[] iv)
        {
            var cipher = new byte[stolen16.Length + encryptedBody.Length];
            Buffer.BlockCopy(stolen16, 0, cipher, 0, stolen16.Length);
            Buffer.BlockCopy(encryptedBody, 0, cipher, stolen16.Length, encryptedBody.Length);
            return DecryptAesCbcWithDerivedIv(key, iv, cipher);
        }

        public static void WriteU32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        public static byte[] Slice(byte[] src, int offset, int length)
        {
            var dst = new byte[length];
            Buffer.BlockCopy(src, offset, dst, 0, length);
            return dst;
        }

        // Embedded SteamDRMP.dll: XTEA per 8-byte block + rolling XOR with 0x55555555 seeds.
        public static void DecryptXteaChained(byte[] data, uint size, uint[] keys)
        {
            uint v1 = 0x55555555;
            uint v2 = 0x55555555;

            for (int x = 0; x + 8 <= (int)size; x += 8)
            {
                uint d1 = BitConverter.ToUInt32(data, x);
                uint d2 = BitConverter.ToUInt32(data, x + 4);
                uint r1, r2;
                XteaDecryptBlock(keys, d1, d2, out r1, out r2);
                WriteU32(data, x, r1 ^ v1);
                WriteU32(data, x + 4, r2 ^ v2);
                v1 = d1;
                v2 = d2;
            }
        }

        private static void XteaDecryptBlock(uint[] keys, uint v1, uint v2, out uint o1, out uint o2)
        {
            const uint delta = 0x9E3779B9;
            uint sum = unchecked(delta * 32);
            for (int x = 0; x < 32; x++)
            {
                v2 = unchecked(v2 - ((((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + keys[(sum >> 11) & 3])));
                sum = unchecked(sum - delta);
                v1 = unchecked(v1 - ((((v2 << 4) ^ (v2 >> 5)) + v2) ^ (sum + keys[sum & 3])));
            }
            o1 = v1;
            o2 = v2;
        }
    }
}
