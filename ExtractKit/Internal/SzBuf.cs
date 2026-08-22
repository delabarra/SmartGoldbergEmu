namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal sealed class CBuf
    {
        public byte[] Data;
        public int Size;

        public void Init()
        {
            Data = null;
            Size = 0;
        }

        public bool Create(int size, ISzAlloc alloc)
        {
            Size = 0;
            if (size == 0)
            {
                Data = null;
                return true;
            }

            Data = SzAllocImpl.Alloc(alloc, size);
            if (Data != null)
            {
                Size = size;
                return true;
            }

            return false;
        }

        public void Free(ISzAlloc alloc)
        {
            SzAllocImpl.Free(alloc, Data);
            Data = null;
            Size = 0;
        }
    }

    internal sealed class CDynBuf
    {
        public byte[] Data;
        public int Size;
        public int Pos;

        public void Construct()
        {
            Data = null;
            Size = 0;
            Pos = 0;
        }

        public void SeekToBeg()
        {
            Pos = 0;
        }

        public int Write(byte[] buf, int size, ISzAlloc alloc)
        {
            if (Pos + size > Size)
            {
                int newSize = Pos + size;
                byte[] newData = SzAllocImpl.Alloc(alloc, newSize);
                if (newData == null)
                    return 0;
                if (Data != null && Size > 0)
                    System.Array.Copy(Data, 0, newData, 0, Size);
                SzAllocImpl.Free(alloc, Data);
                Data = newData;
                Size = newSize;
            }

            System.Array.Copy(buf, 0, Data, Pos, size);
            Pos += size;
            return 1;
        }

        public void Free(ISzAlloc alloc)
        {
            SzAllocImpl.Free(alloc, Data);
            Data = null;
            Size = 0;
            Pos = 0;
        }
    }
}
