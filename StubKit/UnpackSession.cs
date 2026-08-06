using System;

namespace SmartGoldbergEmu.StubKit
{
    // Shared unpack pipeline: decode → optional code decrypt → OEP/TLS → bind finish.
    internal static class UnpackSession
    {
        public static void Run(PeImage pe, DetectResult det, UnpackOptions options, StubUnpackInfo info)
        {
            if (options == null)
                options = UnpackOptions.Default;

            switch (det.Variant)
            {
                case StubVariant.V10_x86:
                {
                    BindAction bind = VariantV10.Remove(pe, options);
                    info.BindAction = bind;
                    info.Summary = Summarize("1.0", false, bind, false);
                    break;
                }

                case StubVariant.V20_x86:
                {
                    var v20 = VariantV20.Read(pe);
                    VariantV20.Remove(pe, v20, options);
                    info.BindAction = v20.BindAction;
                    info.Summary = Summarize("2.0", false, v20.BindAction, false);
                    break;
                }

                case StubVariant.V21_x86:
                {
                    var v21 = VariantV21.Remove(pe, options);
                    info.UsedEncryption = v21.Encrypted;
                    info.BindAction = v21.BindAction;
                    info.Summary = Summarize("2.1", v21.Encrypted, v21.BindAction, false);
                    break;
                }

                case StubVariant.V30_x86:
                case StubVariant.V30_x64:
                case StubVariant.V31_x86:
                case StubVariant.V31_x64:
                {
                    StubHeaderState state = LoadV3Header(pe, det);
                    ApplyV3(pe, state, options);
                    info.UsedEncryption = state.UsedEncryption;
                    info.BindAction = state.BindAction;
                    info.UsedTlsOepOverride = state.UsedTlsOepOverride;
                    string ver = det.Variant == StubVariant.V31_x86 || det.Variant == StubVariant.V31_x64
                        ? "3.1"
                        : "3.0";
                    info.Summary = Summarize(ver, state.UsedEncryption, state.BindAction, state.UsedTlsOepOverride);
                    break;
                }

                default:
                    throw new InvalidOperationException("Removal not implemented for " + det.Name);
            }
        }

        private static StubHeaderState LoadV3Header(PeImage pe, DetectResult det)
        {
            StubHeaderState state;
            uint preferred = det.HeaderSiteRva != 0 ? det.HeaderSiteRva : pe.AddressOfEntryPoint;
            int[] sizes = det.HeaderSize == 0xF0 || det.HeaderSize == 0xD0 || det.HeaderSize == 0xB0
                ? new[] { det.HeaderSize }
                : HeaderSchemas.V3HeaderSizes;

            if (TryLoadAt(pe, preferred, sizes, out state))
                return state;

            if (preferred != pe.AddressOfEntryPoint && TryLoadAt(pe, pe.AddressOfEntryPoint, sizes, out state))
                return state;

            foreach (ulong cb in pe.TlsCallbacks)
            {
                uint rva = pe.VaToRva(cb);
                if (TryLoadAt(pe, rva, sizes, out state))
                {
                    state.UsedTlsSite = true;
                    return state;
                }
            }

            throw new InvalidOperationException("SteamStub 3.x header not found at EP or TLS.");
        }

        private static bool TryLoadAt(PeImage pe, uint site, int[] sizes, out StubHeaderState state)
        {
            state = null;
            foreach (int hs in sizes)
            {
                if (StubClassifier.TryDecodeHeaderAt(pe, site, hs, out state))
                    return true;
            }
            return false;
        }

        private static void ApplyV3(PeImage pe, StubHeaderState state, UnpackOptions options)
        {
            bool isV30 = state.Variant == StubVariant.V30_x86 || state.Variant == StubVariant.V30_x64;
            var bind = pe.FindSection(".bind");
            if (bind == null)
                throw new InvalidOperationException(".bind missing.");

            if (isV30)
            {
                // V3.0: TLS OEP override before code decrypt (matches prior behavior).
                state.ResolvedOep = state.OriginalEntryPoint;
                bool flagged = state.HasTlsCallback == 1;
                if (flagged || TlsOep.FirstCallbackInBind(pe, bind))
                {
                    uint resolved;
                    if (TlsOep.TryApplyOverride(pe, bind, state.OriginalEntryPoint, state.XorKey, out resolved))
                    {
                        state.UsedTlsOepOverride = true;
                        state.ResolvedOep = resolved;
                    }
                }
            }

            state.UsedEncryption = (state.Flags & HeaderSchemas.FlagNoEncryption) == 0;
            if (state.UsedEncryption)
            {
                uint codeRva = state.CodeSectionVirtualAddress;
                if (codeRva == 0)
                    throw new InvalidOperationException("Encrypted stub but code section VA is zero.");

                var codeSec = pe.SectionFromRva(codeRva);
                if (codeSec == null)
                    throw new InvalidOperationException("Code section VA not found.");

                uint rawSize = state.CodeSectionRawSize;
                if (rawSize == 0 || rawSize > codeSec.SizeOfRawData)
                    rawSize = codeSec.SizeOfRawData;
                if (rawSize == 0)
                    throw new InvalidOperationException("Code section has no raw data to decrypt.");

                var enc = new byte[rawSize];
                Buffer.BlockCopy(pe.Data, (int)codeSec.PointerToRawData, enc, 0, (int)rawSize);
                byte[] dec = StubCiphers.DecryptCodeSection(state.StolenData, enc, state.AesKey, state.AesIv);
                Buffer.BlockCopy(dec, 0, pe.Data, (int)codeSec.PointerToRawData, (int)rawSize);
            }

            if (!isV30)
            {
                // V3.1: TLS OEP override after code decrypt.
                uint resolved;
                if (TlsOep.TryApplyOverride(pe, bind, state.OriginalEntryPoint, state.XorKey, out resolved))
                {
                    state.UsedTlsOepOverride = true;
                    state.ResolvedOep = resolved;
                }
                else
                {
                    state.ResolvedOep = state.OriginalEntryPoint;
                }
            }

            pe.AddressOfEntryPoint = state.ResolvedOep;
            pe.CheckSum = 0;
            // Re-find bind — TLS rewrite may have changed section metadata but bind still exists.
            bind = pe.FindSection(".bind");
            if (bind == null)
                throw new InvalidOperationException(".bind missing.");
            state.BindAction = BindFinish.Apply(pe, bind, options);
        }

        private static string Summarize(string ver, bool encrypted, BindAction bind, bool tlsOep)
        {
            string enc = encrypted ? "AES decrypt + " : "";
            string bindStr = BindFinish.Describe(bind) + " + ";
            string oep = tlsOep ? "TLS/OEP override" : "OEP restored";
            return "Removed SteamStub " + ver + " (" + enc + bindStr + oep + ").";
        }
    }
}
