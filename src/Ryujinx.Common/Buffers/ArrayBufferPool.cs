using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Common.Buffers
{

    public sealed class ArrayBufferPool : IBufferPool
    {

        private const int InitialQueueCapacity = 512;

        private readonly int _minPoolIndex;
        private readonly bool _cleanAfterReturn;

        private readonly Lock _poolLock;
        private Queue<byte[]>[] _pool;

        public ArrayBufferPool(int minBufferSize, int initialBufferPools, int initialBuffersPerPool, bool cleanAfterReturn)
        {
            if (minBufferSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minBufferSize), "Must be greater than zero");
            }

            if (initialBufferPools < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialBufferPools), "Must not be negative");
            }

            if (initialBuffersPerPool < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialBuffersPerPool), "Must not be negative");
            }

            _minPoolIndex = BufferPoolUtils.SizeToPoolIndex(minBufferSize);
            _cleanAfterReturn = cleanAfterReturn;

            _poolLock = new Lock();
            _pool = new Queue<byte[]>[initialBufferPools];

            for (int i = 0; i < initialBufferPools; i++)
            {
                _pool[i] = new Queue<byte[]>(InitialQueueCapacity);

                for (int j = 0; j < initialBuffersPerPool; j++)
                {
                    _pool[i].Enqueue(PrivateCreateArray(i));
                }
            }
        }

        public void Dispose() { }

        public void EnsureCapacity(int requestedSize, int requestedCapacity)
        {
            lock (_poolLock)
            {
                PrivateEnsurePoolCapacityLocked(requestedSize, out int poolIndex);

                while (_pool[poolIndex].Count < requestedCapacity)
                {
                    _pool[poolIndex].Enqueue(PrivateCreateArray(poolIndex));
                }
            }
        }

        public IBuffer Rent(int requestedSize)
        {
            if (requestedSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedSize), "Must be greater than zero");
            }

            byte[] buffer;

            lock (_poolLock)
            {
                buffer = PrivateRentUnsafeLocked(requestedSize);
            }

            Debug.Assert(buffer.Length >= requestedSize);

            return new ArrayBuffer(this, buffer, requestedSize);
        }

        public byte[] RentUnsafe(int requestedSize)
        {
            byte[] buffer;

            lock (_poolLock)
            {
                buffer = PrivateRentUnsafeLocked(requestedSize);
            }

            Debug.Assert(buffer.Length >= requestedSize);

            return buffer;
        }

        public void Return(IBuffer buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (buffer is ArrayBuffer arrayBuffer)
            {
                Return(arrayBuffer);
            }
            else
            {
                throw new ArgumentException("Attempted to return incompatible buffer-instance", nameof(buffer));
            }
        }

        public void Return(ArrayBuffer buffer)
        {
            int absolutePoolIndex = BufferPoolUtils.SizeToPoolIndex(buffer.Array.Length);
            if (absolutePoolIndex < _minPoolIndex)
            {
                throw new ArgumentException("Buffer size is less than minimum pool buffer size", nameof(buffer));
            }

            int relativePoolIndex = absolutePoolIndex - _minPoolIndex;

            lock (_poolLock)
            {
                if (_pool.Length <= relativePoolIndex)
                {
                    throw new InvalidOperationException("Attempted to return non-associated buffer to pool");
                }
            }

            if (_cleanAfterReturn)
            {
                unsafe
                {
                    fixed (byte* arrayPtr = buffer.Array)
                    {
                        NativeMemory.Fill(arrayPtr, (nuint)buffer.Array.Length, 0);
                    }
                }
            }

            lock (_poolLock)
            {
                _pool[relativePoolIndex].Enqueue(buffer.Array);
            }
        }

        private byte[] PrivateRentUnsafeLocked(int requestedSize)
        {
            PrivateEnsurePoolCapacityLocked(requestedSize, out int relativePoolIndex);

            if (_pool[relativePoolIndex].TryDequeue(out byte[] buffer))
            {
                return buffer;
            }

            return PrivateCreateArray(relativePoolIndex);
        }

        private void PrivateEnsurePoolCapacityLocked(int requestedSize, out int relativePoolIndex)
        {
            if (requestedSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedSize), "Must be greater than zero");
            }

            int absolutePoolIndex = BufferPoolUtils.SizeToPoolIndex(requestedSize);
            if (absolutePoolIndex < _minPoolIndex)
            {
                absolutePoolIndex = _minPoolIndex;
            }

            relativePoolIndex = absolutePoolIndex - _minPoolIndex;

            if (_pool.Length <= relativePoolIndex)
            {
                int previousPoolLength = _pool.Length;

                Array.Resize(ref _pool, relativePoolIndex + 1);

                for (int i = previousPoolLength; i < _pool.Length; i++)
                {
                    if (_pool[i] == null)
                    {
                        _pool[i] = new Queue<byte[]>(InitialQueueCapacity);
                    }
                }
            }
        }

        private byte[] PrivateCreateArray(int relativePoolIndex)
        {
            return new byte[BufferPoolUtils.PoolIndexToSize(_minPoolIndex + relativePoolIndex)];
        }

    }

}
