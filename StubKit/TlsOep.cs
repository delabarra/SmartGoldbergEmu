using System;
using System.Collections.Generic;

namespace SmartGoldbergEmu.StubKit
{
    // When the stub hijacks TLS[0], the PE entry stub XOR-decodes the real OEP and
    // header.OriginalEntryPoint is the original TLS callback RVA.
    internal static class TlsOep
    {
        public static bool FirstCallbackInBind(PeImage pe, PeImage.Section bind)
        {
            if (bind == null || pe.TlsCallbacks == null || pe.TlsCallbacks.Count == 0)
                return false;
            try
            {
                uint rva = pe.VaToRva(pe.TlsCallbacks[0]);
                uint end = bind.VirtualAddress + Math.Max(bind.VirtualSize, bind.SizeOfRawData);
                return rva >= bind.VirtualAddress && rva < end;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryComputeRealOep(PeImage pe, uint xorKey, out uint realOepRva)
        {
            realOepRva = 0;
            try
            {
                uint entryOff = pe.RvaToOffset(pe.AddressOfEntryPoint);
                int len = (int)Math.Min(0x100u, (uint)(pe.Data.Length - entryOff));
                var data = StubCiphers.Slice(pe.Data, (int)entryOff, len);

                int res = ByteMask.Find(data, ByteMask.TlsOepX64);
                int immAt = 0x0B;
                if (res < 0)
                {
                    res = ByteMask.Find(data, ByteMask.TlsOepX86);
                    immAt = 0x0A;
                }
                if (res < 0)
                    return false;

                int imm = BitConverter.ToInt32(data, res + immAt);
                long mixed = (long)xorKey ^ imm;
                ulong key = unchecked((ulong)mixed);
                ulong epVa = pe.ImageBase + pe.AddressOfEntryPoint;
                ulong off = unchecked(epVa + key);
                realOepRva = (uint)(off - pe.ImageBase);
                return realOepRva != 0 && pe.SectionFromRva(realOepRva) != null;
            }
            catch
            {
                return false;
            }
        }

        // Restore TLS[0] to the pre-stub callback (header OEP RVA); set resolvedEp to XOR-decoded OEP.
        public static bool TryApplyOverride(PeImage pe, PeImage.Section bind, uint headerOepRva, uint xorKey, out uint resolvedEp)
        {
            resolvedEp = headerOepRva;
            if (!FirstCallbackInBind(pe, bind))
                return false;

            uint realOep;
            if (!TryComputeRealOep(pe, xorKey, out realOep))
                return false;

            ulong restored = pe.ImageBase + headerOepRva;
            var list = new List<ulong>();
            list.Add(restored);
            uint bindStart = bind.VirtualAddress;
            uint bindEnd = bindStart + Math.Max(bind.VirtualSize, bind.SizeOfRawData);

            foreach (ulong cb in pe.TlsCallbacks)
            {
                uint rva;
                try { rva = pe.VaToRva(cb); }
                catch { continue; }
                if (rva >= bindStart && rva < bindEnd)
                    continue;
                if (cb == restored)
                    continue;
                list.Add(cb);
            }

            pe.WriteTlsCallbacks(list);
            resolvedEp = realOep;
            return true;
        }
    }
}
