using System;

namespace SmartGoldbergEmu.StubKit
{
    // SteamStub 1.0 (x86): index-squared XOR header; OEP from bind epilogue.
    internal static class VariantV10
    {
        public static BindAction Remove(PeImage pe, UnpackOptions options = null)
        {
            var bind = pe.FindSection(".bind");
            if (bind == null)
                throw new InvalidOperationException(".bind missing.");

            var bindData = new byte[bind.SizeOfRawData];
            Buffer.BlockCopy(pe.Data, (int)bind.PointerToRawData, bindData, 0, bindData.Length);

            int offset = ByteMask.Find(bindData, ByteMask.V10BindPrologue);
            if (offset < 0)
                throw new InvalidOperationException("V1.0 header pattern not found.");

            uint headerPointer = BitConverter.ToUInt32(bindData, offset + 8);
            uint headerSize = BitConverter.ToUInt32(bindData, offset + 13) * 4;
            uint fileOffset = pe.RvaToOffset(headerPointer - (uint)pe.ImageBase);

            var headerData = new byte[headerSize];
            Buffer.BlockCopy(pe.Data, (int)fileOffset, headerData, 0, (int)headerSize);
            StubCiphers.XorByIndexSquared(headerData);

            uint bindFunction = BitConverter.ToUInt32(headerData, 0x08);
            if (bindFunction - (uint)pe.ImageBase != pe.AddressOfEntryPoint)
                throw new InvalidOperationException("V1.0 header BindFunction does not match EP.");

            int oepPat = ByteMask.Find(bindData, ByteMask.V10OepEpilogue);
            if (oepPat < 0)
                throw new InvalidOperationException("V1.0 OEP pattern not found.");

            uint oepVa = BitConverter.ToUInt32(bindData, oepPat + 2);
            pe.AddressOfEntryPoint = oepVa - (uint)pe.ImageBase;
            pe.CheckSum = 0;
            return BindFinish.Apply(pe, bind, options);
        }
    }
}
