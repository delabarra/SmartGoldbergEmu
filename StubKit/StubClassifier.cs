using System;
using System.Collections.Generic;

namespace SmartGoldbergEmu.StubKit
{
    public enum StubVariant
    {
        None = 0,
        V10_x86,
        V20_x86,
        V21_x86,
        V30_x86,
        V30_x64,
        V31_x86,
        V31_x64
    }

    public sealed class DetectResult
    {
        public StubVariant Variant;
        public int HeaderSize;
        public string Name;
        public bool CanRemove;
        public bool UsedAutoProbe;
        // Stub entry RVA where the DRM header sits immediately before (3.x).
        public uint HeaderSiteRva;
    }

    // Probe-first classification: recover header signatures at EP/TLS, then EP immediates, then bind masks.
    internal static class StubClassifier
    {
        public static DetectResult Detect(PeImage pe)
        {
            DetectResult probed = TryHeaderProbe(pe);
            if (probed.Variant != StubVariant.None)
            {
                probed.UsedAutoProbe = true;
                return probed;
            }

            DetectResult fromEp = TryEpImmediateVariants(pe);
            if (fromEp.Variant != StubVariant.None)
            {
                fromEp.UsedAutoProbe = true;
                return fromEp;
            }

            DetectResult fromBind = TryBindMaskHints(pe);
            if (fromBind.Variant != StubVariant.None)
                return fromBind;

            var bind = pe.FindSection(".bind");
            if (bind != null)
            {
                var epSec = pe.SectionFromRva(pe.AddressOfEntryPoint);
                if (epSec != null && epSec.Name == ".bind")
                {
                    return new DetectResult
                    {
                        Variant = StubVariant.None,
                        Name = "unknown (.bind + EP in .bind)",
                        CanRemove = false
                    };
                }
            }

            return new DetectResult { Variant = StubVariant.None, Name = "none", CanRemove = false };
        }

        private static DetectResult TryHeaderProbe(PeImage pe)
        {
            var sites = new List<uint> { pe.AddressOfEntryPoint };
            foreach (ulong cb in pe.TlsCallbacks)
            {
                try { sites.Add(pe.VaToRva(cb)); }
                catch { }
            }

            foreach (uint site in sites)
            {
                foreach (int hs in HeaderSchemas.V3HeaderSizes)
                {
                    StubHeaderState state;
                    if (!TryDecodeHeaderAt(pe, site, hs, out state))
                        continue;

                    bool usedTls = site != pe.AddressOfEntryPoint;
                    return Make(
                        state.Variant,
                        hs,
                        FormatName(state.Variant),
                        site,
                        usedTls);
                }
            }

            return new DetectResult { Variant = StubVariant.None };
        }

        private static DetectResult TryEpImmediateVariants(PeImage pe)
        {
            if (pe.IsPe32Plus)
                return new DetectResult { Variant = StubVariant.None };

            try
            {
                uint epOff = pe.RvaToOffset(pe.AddressOfEntryPoint);
                if (epOff < 4 || BitConverter.ToUInt32(pe.Data, (int)epOff - 4) != HeaderSchemas.SigV30)
                    return new DetectResult { Variant = StubVariant.None };

                EpImmediateScanner.Result scan;
                if (EpImmediateScanner.TryScanV21(pe, out scan))
                    return Make(StubVariant.V21_x86, (int)scan.StructSize, "SteamStub 2.1 (x86)", pe.AddressOfEntryPoint, false);
                if (EpImmediateScanner.TryScanV20(pe, out scan))
                    return Make(StubVariant.V20_x86, (int)scan.StructSize, "SteamStub 2.0 (x86)", pe.AddressOfEntryPoint, false);
            }
            catch { }

            return new DetectResult { Variant = StubVariant.None };
        }

        private static DetectResult TryBindMaskHints(PeImage pe)
        {
            var bind = pe.FindSection(".bind");
            if (bind == null)
                return new DetectResult { Variant = StubVariant.None };

            int len = (int)Math.Min(bind.SizeOfRawData, 0x4000u);
            var bindData = new byte[len];
            Buffer.BlockCopy(pe.Data, (int)bind.PointerToRawData, bindData, 0, len);

            if (!pe.IsPe32Plus)
            {
                if (ByteMask.Find(bindData, ByteMask.V10BindPrologue) >= 0)
                    return Make(StubVariant.V10_x86, 0, "SteamStub 1.0 (x86)", pe.AddressOfEntryPoint, false);

                // Bind prologue can confirm 3.x when header size can be recovered from code.
                if (ByteMask.Find(bindData, ByteMask.V3x86BindPrologue) >= 0)
                {
                    int hs = ResolveHeaderSizeFromBindX86(bindData);
                    if (hs == 0)
                        hs = ProbeHeaderSizeAtSites(pe);
                    if (hs == 0xF0)
                        return Make(StubVariant.V31_x86, hs, "SteamStub 3.1 (x86)", pe.AddressOfEntryPoint, false);
                    if (hs == 0xB0 || hs == 0xD0)
                        return Make(StubVariant.V30_x86, hs, "SteamStub 3.0 (x86)", pe.AddressOfEntryPoint, false);
                }

                if (ByteMask.Find(bindData, ByteMask.V21BindPrologue) >= 0)
                    return Make(StubVariant.V21_x86, 0, "SteamStub 2.1 (x86)", pe.AddressOfEntryPoint, false);
                if (ByteMask.Find(bindData, ByteMask.V20BindPrologue) >= 0)
                    return Make(StubVariant.V20_x86, 0, "SteamStub 2.0 (x86)", pe.AddressOfEntryPoint, false);
            }
            else if (ByteMask.Find(bindData, ByteMask.V3x64BindPrologue) >= 0)
            {
                int hs = ResolveHeaderSizeFromBindX64(bindData);
                if (hs == 0)
                    hs = ProbeHeaderSizeAtSites(pe);
                if (hs == 0xF0)
                    return Make(StubVariant.V31_x64, hs, "SteamStub 3.1 (x64)", pe.AddressOfEntryPoint, false);
                if (hs == 0xB0 || hs == 0xD0)
                    return Make(StubVariant.V30_x64, hs, "SteamStub 3.0 (x64)", pe.AddressOfEntryPoint, false);
            }

            return new DetectResult { Variant = StubVariant.None };
        }

        internal static bool TryDecodeHeaderAt(PeImage pe, uint stubRva, int headerSize, out StubHeaderState state)
        {
            state = null;
            try
            {
                uint off = pe.RvaToOffset(stubRva);
                if (off < headerSize)
                    return false;
                var raw = new byte[headerSize];
                Buffer.BlockCopy(pe.Data, (int)(off - headerSize), raw, 0, headerSize);
                StubCiphers.DecodeChainedDwords(raw, (uint)headerSize, 0);
                if (!HeaderSchemas.TryParseV3(raw, headerSize, pe.IsPe32Plus, stubRva, out state))
                    return false;
                state.UsedTlsSite = stubRva != pe.AddressOfEntryPoint;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ProbeHeaderSizeAtSites(PeImage pe)
        {
            foreach (int hs in HeaderSchemas.V3HeaderSizes)
            {
                StubHeaderState state;
                if (TryDecodeHeaderAt(pe, pe.AddressOfEntryPoint, hs, out state))
                    return hs;
                foreach (ulong cb in pe.TlsCallbacks)
                {
                    try
                    {
                        if (TryDecodeHeaderAt(pe, pe.VaToRva(cb), hs, out state))
                            return hs;
                    }
                    catch { }
                }
            }
            return 0;
        }

        private static int ResolveHeaderSizeFromBindX86(byte[] bind)
        {
            var tries = new[]
            {
                new { Pat = ByteMask.Compile("55 8B EC 81 EC ?? ?? ?? ?? 53 ?? ?? ?? ?? ?? 68"), Off = 0x10 },
                new { Pat = ByteMask.Compile("55 8B EC 81 EC ?? ?? ?? ?? 53 ?? ?? ?? ?? ?? 8D 83"), Off = 0x16 },
                new { Pat = ByteMask.Compile("55 8B EC 81 EC ?? ?? ?? ?? 56 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 8D"), Off = 0x10 }
            };

            foreach (var t in tries)
            {
                int offset = ByteMask.Find(bind, t.Pat);
                if (offset < 0)
                    continue;
                int hs = BitConverter.ToInt32(bind, offset + t.Off);
                if (hs == 0xB0 || hs == 0xD0 || hs == 0xF0)
                    return hs;
            }
            return 0;
        }

        private static int ResolveHeaderSizeFromBindX64(byte[] bind)
        {
            var lea1 = ByteMask.Compile("48 8D 91 ?? ?? ?? ?? 48");
            var lea2 = ByteMask.Compile("48 8D 91 ?? ?? ?? ?? 41");
            var mov = ByteMask.Compile("48 C7 84 24 ?? ?? ?? ?? ?? ?? ?? ?? 48");

            int offset = ByteMask.Find(bind, lea1);
            if (offset < 0)
                offset = ByteMask.Find(bind, lea2);
            if (offset < 0)
            {
                offset = ByteMask.Find(bind, mov);
                if (offset >= 0)
                    offset += 5;
            }
            if (offset < 0)
                return 0;

            int hs = Math.Abs(BitConverter.ToInt32(bind, offset + 3));
            if (hs == 0xB0 || hs == 0xD0 || hs == 0xF0)
                return hs;
            return 0;
        }

        private static string FormatName(StubVariant v)
        {
            switch (v)
            {
                case StubVariant.V31_x86: return "SteamStub 3.1 (x86)";
                case StubVariant.V31_x64: return "SteamStub 3.1 (x64)";
                case StubVariant.V30_x86: return "SteamStub 3.0 (x86)";
                case StubVariant.V30_x64: return "SteamStub 3.0 (x64)";
                default: return v.ToString();
            }
        }

        private static DetectResult Make(StubVariant v, int hs, string name, uint siteRva, bool usedTls)
        {
            return new DetectResult
            {
                Variant = v,
                HeaderSize = hs,
                Name = name,
                CanRemove = true,
                HeaderSiteRva = siteRva,
                UsedAutoProbe = usedTls
            };
        }
    }
}
