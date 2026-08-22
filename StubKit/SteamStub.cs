using System;
using System.IO;

namespace SmartGoldbergEmu.StubKit
{
    // In-process SteamStub detect / remove.
    public static class SteamStub
    {
        // Cap for contiguous detect prefix. Larger spans use headers-only (.bind) classification.
        private const int DetectPrefixCapBytes = 512 * 1024;

        // Read-only: does not copy or mutate peBytes (classifier only inspects the image).
        public static DetectResult Detect(byte[] peBytes)
        {
            if (peBytes == null || peBytes.Length == 0)
            {
                return new DetectResult
                {
                    Variant = StubVariant.None,
                    Name = "none",
                    CanRemove = false
                };
            }

            try
            {
                PeImage pe = PeImage.Load(peBytes);
                return StubClassifier.Detect(pe);
            }
            catch (Exception)
            {
                return new DetectResult
                {
                    Variant = StubVariant.None,
                    Name = "invalid PE",
                    CanRemove = false
                };
            }
        }

        // Detect without loading the whole file: reads a PE prefix then StubClassifier.Detect.
        public static DetectResult DetectFile(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return new DetectResult
                {
                    Variant = StubVariant.None,
                    Name = "none",
                    CanRemove = false
                };
            }

            try
            {
                using (var fs = new FileStream(
                    executablePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite))
                {
                    int needed = PeImage.GetDetectPrefixLength(fs);
                    if (needed <= 0)
                    {
                        return new DetectResult
                        {
                            Variant = StubVariant.None,
                            Name = "invalid PE",
                            CanRemove = false
                        };
                    }

                    // Stub data spans a large region: do not allocate a multi‑MB prefix for menus.
                    // Section table (.bind presence) is enough to offer Patch; Apply re-detects fully.
                    if (needed > DetectPrefixCapBytes)
                        return DetectFromHeadersOnly(fs);

                    int readLen = (int)Math.Min(needed, fs.Length);
                    byte[] prefix = new byte[readLen];
                    fs.Position = 0;
                    PeImage.ReadExact(fs, prefix, readLen);

                    DetectResult detect = Detect(prefix);
                    prefix = null;
                    return detect;
                }
            }
            catch (Exception)
            {
                return new DetectResult
                {
                    Variant = StubVariant.None,
                    Name = "unreadable",
                    CanRemove = false
                };
            }
        }

        private static DetectResult DetectFromHeadersOnly(FileStream fs)
        {
            int headerLen = (int)Math.Min(fs.Length, 256 * 1024);
            byte[] headers = new byte[headerLen];
            fs.Position = 0;
            PeImage.ReadExact(fs, headers, headerLen);

            try
            {
                PeImage pe = PeImage.Load(headers);
                if (pe.FindSection(".bind") != null)
                {
                    return new DetectResult
                    {
                        Variant = StubVariant.None,
                        Name = "SteamStub (.bind)",
                        CanRemove = true
                    };
                }

                return new DetectResult
                {
                    Variant = StubVariant.None,
                    Name = "none",
                    CanRemove = false
                };
            }
            catch (Exception)
            {
                return new DetectResult
                {
                    Variant = StubVariant.None,
                    Name = "invalid PE",
                    CanRemove = false
                };
            }
        }

        public static bool TryUnpack(
            byte[] peBytes,
            UnpackOptions options,
            out byte[] unpacked,
            out StubUnpackInfo info)
        {
            return TryUnpack(peBytes, options, mutateInPlace: false, out unpacked, out info);
        }

        // mutateInPlace: rewrite peBytes directly (caller owns the buffer; no second PE-sized copy).
        public static bool TryUnpack(
            byte[] peBytes,
            UnpackOptions options,
            bool mutateInPlace,
            out byte[] unpacked,
            out StubUnpackInfo info)
        {
            unpacked = null;
            info = new StubUnpackInfo();

            if (peBytes == null || peBytes.Length == 0)
            {
                info.ErrorMessage = "Executable data is empty.";
                return false;
            }

            byte[] working;
            PeImage pe;
            DetectResult det;
            try
            {
                working = mutateInPlace ? peBytes : (byte[])peBytes.Clone();
                pe = PeImage.Load(working);
                det = StubClassifier.Detect(pe);
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
                return false;
            }

            info.Variant = det.Variant;
            info.VariantName = det.Name;

            if (det.Variant == StubVariant.None)
            {
                info.ErrorMessage = "No supported SteamStub signature matched.";
                return false;
            }

            if (!det.CanRemove)
            {
                info.ErrorMessage = "This SteamStub variant cannot be removed.";
                return false;
            }

            try
            {
                UnpackSession.Run(pe, det, options ?? UnpackOptions.Default, info);
                unpacked = pe.Data;
                info.NewEntryPointRva = pe.AddressOfEntryPoint;
                return true;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
                unpacked = null;
                return false;
            }
        }
    }
}
