using System;

namespace Ryujinx.Common.Buffers
{

    public interface IBufferPool : IDisposable
    {

        void EnsureCapacity(int requestedSize, int requestedCapacity);

        IBuffer Rent(int requestedSize);

        void Return(IBuffer buffer);

    }

}
