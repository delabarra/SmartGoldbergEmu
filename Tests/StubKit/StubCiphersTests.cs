using System;
using System.Security.Cryptography;
using SmartGoldbergEmu.StubKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.StubKit
{
    public sealed class StubCiphersTests
    {
        [Fact]
        public void DecodeChainedDwords_with_explicit_key_recovers_plaintext()
        {
            // Encrypt with same chaining rule as decrypt: next key is ciphertext DWORD.
            uint key = 0xA5A5A5A5;
            uint p0 = 0x11223344;
            uint p1 = 0x55667788;
            uint c0 = p0 ^ key;
            uint c1 = p1 ^ c0;

            var data = new byte[8];
            StubCiphers.WriteU32(data, 0, c0);
            StubCiphers.WriteU32(data, 4, c1);

            StubCiphers.DecodeChainedDwords(data, 8, key);
            Assert.Equal(p0, BitConverter.ToUInt32(data, 0));
            Assert.Equal(p1, BitConverter.ToUInt32(data, 4));
        }

        [Fact]
        public void DecodeChainedDwords_seed_from_first_dword_when_key_zero()
        {
            // Layout: [seed][c0][c1] — seed stays; chain key updates from ciphertext.
            uint seed = 0x01020304;
            uint p0 = 0x11111111;
            uint p1 = 0x22222222;
            uint c0 = p0 ^ seed;
            uint c1 = p1 ^ c0;
            var data = new byte[12];
            StubCiphers.WriteU32(data, 0, seed);
            StubCiphers.WriteU32(data, 4, c0);
            StubCiphers.WriteU32(data, 8, c1);

            StubCiphers.DecodeChainedDwords(data, 12, 0);
            Assert.Equal(seed, BitConverter.ToUInt32(data, 0));
            Assert.Equal(p0, BitConverter.ToUInt32(data, 4));
            Assert.Equal(p1, BitConverter.ToUInt32(data, 8));
        }

        [Fact]
        public void XorByIndexSquared_is_involutory()
        {
            var data = new byte[] { 0x00, 0x10, 0x20, 0x30, 0x40, 0x50 };
            var copy = (byte[])data.Clone();
            StubCiphers.XorByIndexSquared(data);
            StubCiphers.XorByIndexSquared(data);
            Assert.True(BytesEqual(copy, data));
        }

        [Fact]
        public void DecryptAesCbcWithDerivedIv_matches_manual_ecb_iv_then_cbc()
        {
            var key = new byte[32];
            var storedIv = new byte[16];
            for (int i = 0; i < key.Length; i++)
                key[i] = (byte)(i + 1);
            for (int i = 0; i < storedIv.Length; i++)
                storedIv[i] = (byte)(0xA0 + i);

            var plain = new byte[32];
            for (int i = 0; i < plain.Length; i++)
                plain[i] = (byte)(0x40 + i);

            byte[] derivedIv = (byte[])storedIv.Clone();
            using (var aes = new AesCryptoServiceProvider())
            {
                aes.Key = key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.IV = new byte[16];
                using (var dec = aes.CreateDecryptor())
                {
                    var tmp = new byte[16];
                    Assert.Equal(16, dec.TransformBlock(derivedIv, 0, 16, tmp, 0));
                    Buffer.BlockCopy(tmp, 0, derivedIv, 0, 16);
                }
            }

            byte[] cipher;
            using (var aes = new AesCryptoServiceProvider())
            {
                aes.Key = key;
                aes.IV = derivedIv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;
                using (var enc = aes.CreateEncryptor())
                    cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
            }

            byte[] got = StubCiphers.DecryptAesCbcWithDerivedIv(key, storedIv, cipher);
            Assert.True(BytesEqual(plain, got));
        }

        [Fact]
        public void ByteMask_Find_respects_wildcards()
        {
            var hay = new byte[] { 0x00, 0x60, 0x81, 0xEC, 0x00, 0x10, 0x00, 0x00, 0xBE, 0x12, 0x34, 0x56, 0x78, 0xB9, 0x6A };
            int hit = ByteMask.Find(hay, ByteMask.V10BindPrologue);
            Assert.Equal(1, hit);
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
    }
}
