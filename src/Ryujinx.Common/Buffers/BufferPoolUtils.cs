using System;

namespace Ryujinx.Common.Buffers
{

    static class BufferPoolUtils
    {

        public static int SizeToPoolIndex(long size)
            => (int)Math.Ceiling(Math.Log2(size));

        public static int PoolIndexToSize(int poolIndex)
            => (int)Math.Pow(2, poolIndex);

    }

}
