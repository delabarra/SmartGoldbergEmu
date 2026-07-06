namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class Delta
    {
        public const int StateSize = 256;

        public static void Init(byte[] state)
        {
            for (int i = 0; i < StateSize; i++)
                state[i] = 0;
        }

        public static void Encode(byte[] state, uint delta, byte[] data, int size)
        {
            if (size == 0)
                return;

            byte[] temp = new byte[StateSize];
            for (uint i = 0; i < delta; i++)
                temp[i] = state[i];

            if (size <= delta)
            {
                uint i = 0;
                for (int d = 0; d < size; d++)
                {
                    byte b = data[d];
                    data[d] = (byte)(b - temp[i]);
                    temp[i] = b;
                    i++;
                }

                uint k = 0;
                do
                {
                    if (i == delta)
                        i = 0;
                    state[k] = temp[i++];
                }
                while (++k != delta);
                return;
            }

            int p = size - (int)delta;
            for (uint i = 0; i < delta; i++)
                state[i] = data[p++];

            int lim = (int)delta;
            int dif = -(int)delta;
            if (((size + dif) & 1) != 0)
            {
                p--;
                data[p] = (byte)(data[p] - data[p + dif]);
            }

            while (p != lim)
            {
                p--;
                data[p] = (byte)(data[p] - data[p + dif]);
                p--;
                data[p] = (byte)(data[p] - data[p + dif]);
            }

            dif = -dif;
            do
            {
                p--;
                data[p] = (byte)(data[p] - temp[--dif]);
            }
            while (dif != 0);
        }

        public static void Decode(byte[] state, uint delta, byte[] data, int size)
        {
            if (size == 0)
                return;

            int i = 0;
            int lim = size;
            int d = 0;
            int stateOff = 0;

            if (size <= (int)delta)
            {
                do
                {
                    data[d] = (byte)(data[d] + state[i++]);
                }
                while (++d != lim);

                uint dl = delta;
                for (; dl != (uint)i; stateOff++, dl--)
                    state[stateOff] = state[stateOff + i];
                d -= i;
            }
            else
            {
                do
                {
                    data[d] = (byte)(data[d] + state[i++]);
                    d++;
                }
                while (i != (int)delta);

                int dif = -(int)delta;
                do
                {
                    data[d] = (byte)(data[d] + data[d + dif]);
                }
                while (++d != lim);
                d += dif;
            }

            do
            {
                state[stateOff++] = data[d];
            }
            while (++d != lim);
        }
    }
}
