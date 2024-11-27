using Ryujinx.Common.Memory;
using System;
using System.Threading;

namespace Ryujinx.Common.Buffers.Unsafe
{

    public sealed unsafe class UnsafeBuffer : IBuffer
    {

        private readonly UnsafeBufferPool _pool;

        private int _disposed = 0;

        public int MemorySize { get; }

        public int Length { get; }

        public nint Pointer { get; }

        public ReadOnlySpan<byte> ReadOnlySpan
        {
            get => new ReadOnlySpan<byte>((byte*)Pointer, Length);
        }

        public Span<byte> Span
        {
            get => new Span<byte>((byte*)Pointer, Length);
        }

        internal UnsafeBuffer(UnsafeBufferPool pool, nint pointer, int memorySize, int length)
        {
            _pool = pool;

            Pointer = pointer;
            MemorySize = memorySize;
            Length = length;
        }

        public unsafe void Fixed(Action<nint> callback)
        {
            callback(Pointer);
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                _pool.Return(this);
                GC.SuppressFinalize(this);
            }
        }

        public static implicit operator ReadOnlySpan<byte>(UnsafeBuffer rentedBuffer)
            => rentedBuffer.ReadOnlySpan;

        public static implicit operator Span<byte>(UnsafeBuffer rentedBuffer)
            => rentedBuffer.Span;

        public static implicit operator byte*(UnsafeBuffer rentedBuffer)
            => (byte*)rentedBuffer.Pointer;

        public static implicit operator nuint(UnsafeBuffer rentedBuffer)
            => (nuint)rentedBuffer.Pointer;

        public static implicit operator nint(UnsafeBuffer rentedBuffer)
            => rentedBuffer.Pointer;

    }

}
