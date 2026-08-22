using System;
using System.Collections.Generic;

namespace SmartGoldbergEmu.StubKit
{
    // SteamStub 2.1 (x86): chained XOR header/payload, embedded DRMP (XTEA), optional AES.
    internal static class VariantV21
    {
        public sealed class Info
        {
            public uint SteamAppId;
            public uint Flags;
            public uint OepRva;
            public uint CodeSectionRva;
            public uint EncryptedSize;
            public bool Encrypted;
            public int HeaderSize;
            public BindAction BindAction;
        }

        public static Info Remove(PeImage pe, UnpackOptions options = null)
        {
            EpImmediateScanner.Result scan;
            if (!EpImmediateScanner.TryScanV21(pe, out scan))
                throw new InvalidOperationException("Could not locate V2.1 DRM header from EP.");

            var header = new byte[scan.StructSize];
            Buffer.BlockCopy(pe.Data, (int)pe.RvaToOffset(scan.StructRva), header, 0, (int)scan.StructSize);
            uint headerXorLeftover = StubCiphers.DecodeChainedDwords(header, scan.StructSize, scan.XorKey);

            bool d0 = (scan.StructSize / 4) == 0xD0;
            int offPayloadVa = d0 ? 0x20 : 0x24;
            int offPayloadSize = d0 ? 0x24 : 0x28;
            int offAppId = d0 ? 0x28 : 0x2C;
            int offKeyMatch = d0 ? 0x1C : 0x20;
            int offDrmpVa = d0 ? 0x38 : 0x3C;
            int offDrmpSize = d0 ? 0x3C : 0x40;
            int offXtea = d0 ? 0x40 : 0x44;

            uint keyMatch = BitConverter.ToUInt32(header, offKeyMatch);
            uint payloadVa = BitConverter.ToUInt32(header, offPayloadVa);
            uint payloadSize = BitConverter.ToUInt32(header, offPayloadSize);
            uint appId = BitConverter.ToUInt32(header, offAppId);
            uint drmpVaField = BitConverter.ToUInt32(header, offDrmpVa);
            uint drmpSizeField = BitConverter.ToUInt32(header, offDrmpSize);
            uint xteaField = BitConverter.ToUInt32(header, offXtea);

            uint payloadRva = pe.VaToRva(payloadVa);
            var payload = new byte[payloadSize];
            Buffer.BlockCopy(pe.Data, (int)pe.RvaToOffset(payloadRva), payload, 0, (int)payloadSize);

            // Some builds leave the key-match DWORD plaintext and XOR the rest with key=0.
            // Others continue the header rolling-XOR chain.
            if (BitConverter.ToUInt32(payload, 0) == keyMatch)
                StubCiphers.DecodeChainedDwords(payload, payloadSize, 0);
            else
                StubCiphers.DecodeChainedDwords(payload, payloadSize, headerXorLeftover);

            if (BitConverter.ToUInt32(payload, 0) != keyMatch)
                throw new InvalidOperationException("V2.1 payload KeyMatch mismatch after XOR.");

            if (drmpVaField + 4 > payload.Length || drmpSizeField + 4 > payload.Length || xteaField >= payload.Length)
                throw new InvalidOperationException("V2.1 DRMP field offsets out of payload range.");

            uint drmpVa = BitConverter.ToUInt32(payload, (int)drmpVaField);
            uint drmpSize = BitConverter.ToUInt32(payload, (int)drmpSizeField);
            var drmp = new byte[drmpSize];
            Buffer.BlockCopy(pe.Data, (int)pe.RvaToOffset(pe.VaToRva(drmpVa)), drmp, 0, (int)drmpSize);

            int keyCount = (int)((payload.Length - xteaField) / 4);
            if (keyCount < 4)
                throw new InvalidOperationException("V2.1 XTEA key block too small.");

            var xteaKeys = new uint[Math.Max(keyCount, 4)];
            for (int i = 0; i < keyCount; i++)
                xteaKeys[i] = BitConverter.ToUInt32(payload, (int)xteaField + i * 4);

            StubCiphers.DecryptXteaChained(drmp, drmpSize, xteaKeys);
            if (drmp.Length < 0x40 || drmp[0] != (byte)'M' || drmp[1] != (byte)'Z')
                throw new InvalidOperationException("V2.1 SteamDRMP.dll decrypt failed (no MZ).");

            List<int> offsets;
            bool fallback;
            if (!DrmpFieldReader.TryGetOffsets(drmp, out offsets, out fallback))
                throw new InvalidOperationException("V2.1 could not scrape SteamDRMP.dll offsets.");

            uint flags = ReadPayloadU32(payload, offsets[0]);
            uint oepVa = ReadPayloadU32(payload, offsets[2]);
            uint codeVa = offsets[3] != 0 ? ReadPayloadU32(payload, offsets[3]) : 0;
            uint encSize = offsets[4] != 0 ? ReadPayloadU32(payload, offsets[4]) : 0;

            bool encrypted = (flags & HeaderSchemas.FlagNoEncryption) == 0;
            uint codeRva = 0;

            if (encrypted)
            {
                if (codeVa != 0)
                    codeRva = pe.VaToRva(codeVa);
                else
                {
                    int opt = pe.PeOffset + 24;
                    codeRva = BitConverter.ToUInt32(pe.Data, opt + 20);
                }

                var codeSec = pe.SectionFromRva(codeRva);
                if (codeSec == null)
                    throw new InvalidOperationException("V2.1 code section not found.");

                byte[] aesKey = StubCiphers.Slice(payload, offsets[5], 32);
                byte[] aesIv = StubCiphers.Slice(payload, offsets[6], 16);
                byte[] stolen = StubCiphers.Slice(payload, offsets[7], 16);

                if (encSize == 0 || encSize > codeSec.SizeOfRawData)
                    encSize = codeSec.SizeOfRawData;

                var enc = new byte[encSize];
                Buffer.BlockCopy(pe.Data, (int)codeSec.PointerToRawData, enc, 0, (int)encSize);
                byte[] dec = StubCiphers.DecryptCodeSection(stolen, enc, aesKey, aesIv);
                // Write encSize only (stolen+enc decrypts longer; trailing block is often padding).
                Buffer.BlockCopy(dec, 0, pe.Data, (int)codeSec.PointerToRawData, (int)encSize);
            }

            uint oepRva = pe.VaToRva(oepVa);
            pe.AddressOfEntryPoint = oepRva;
            pe.CheckSum = 0;

            var bind = pe.FindSection(".bind");
            if (bind == null)
                throw new InvalidOperationException(".bind missing.");
            var bindAction = BindFinish.Apply(pe, bind, options);

            return new Info
            {
                SteamAppId = appId,
                Flags = flags,
                OepRva = oepRva,
                CodeSectionRva = codeRva,
                EncryptedSize = encSize,
                Encrypted = encrypted,
                HeaderSize = (int)scan.StructSize,
                BindAction = bindAction
            };
        }

        private static uint ReadPayloadU32(byte[] payload, int offset)
        {
            if (offset < 0 || offset + 4 > payload.Length)
                throw new InvalidOperationException("SteamDRMP payload offset out of range: 0x" + offset.ToString("X"));
            return BitConverter.ToUInt32(payload, offset);
        }
    }
}
