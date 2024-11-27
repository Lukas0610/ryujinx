using System;
using System.Threading;

namespace Ryujinx.Common.Buffers
{

    public sealed class ArrayBuffer : IBuffer
    {

        private readonly ArrayBufferPool _pool;

        private int _disposed = 0;

        public int Length { get; }

        public int MemorySize { get; }

        public byte[] Array { get; }

        public ReadOnlySpan<byte> ReadOnlySpan
        {
            get => new ReadOnlySpan<byte>(Array, 0, Length);
        }

        public Span<byte> Span
        {
            get => new Span<byte>(Array, 0, Length);
        }

        internal ArrayBuffer(ArrayBufferPool pool, byte[] buffer, int length)
        {
            _pool = pool;

            Array = buffer;
            MemorySize = buffer.Length;
            Length = length;
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                _pool.Return(this);
                GC.SuppressFinalize(this);
            }
        }

        public unsafe void Fixed(Action<nint> callback)
        {
            fixed (byte* arrayPtr = Array)
            {
                callback((nint)arrayPtr);
            }
        }

        public static implicit operator ReadOnlySpan<byte>(ArrayBuffer rentedBuffer)
            => rentedBuffer.ReadOnlySpan;

        public static implicit operator Span<byte>(ArrayBuffer rentedBuffer)
            => rentedBuffer.Span;

    }

}
