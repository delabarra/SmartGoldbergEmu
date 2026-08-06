using System;

namespace SmartGoldbergEmu.StubKit
{
    // In-process SteamStub detect / remove.
    public static class SteamStub
    {
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
                byte[] working = (byte[])peBytes.Clone();
                PeImage pe = PeImage.Load(working);
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

        public static bool TryUnpack(
            byte[] peBytes,
            UnpackOptions options,
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
                working = (byte[])peBytes.Clone();
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
