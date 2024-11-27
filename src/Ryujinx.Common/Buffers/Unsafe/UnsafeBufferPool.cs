using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Common.Buffers.Unsafe
{

    public sealed unsafe class UnsafeBufferPool : IBufferPool
    {

        private const int InitialQueueCapacity = 512;

        private readonly int _minPoolIndex;
        private readonly bool _cleanAfterReturn;

        private readonly Lock _poolLock;
        private Queue<nint>[] _pool;
        private List<nint>[] _rentTracker;
        private int[] _bufferCounts;

        public UnsafeBufferPool(int minBufferSize, int initialBufferPools, int initialBuffersPerPool, bool cleanAfterReturn)
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
            _pool = new Queue<nint>[initialBufferPools];
            _rentTracker = new List<nint>[initialBufferPools];
            _bufferCounts = new int[initialBufferPools];

            for (int i = 0; i < initialBufferPools; i++)
            {
                _pool[i] = new Queue<nint>(InitialQueueCapacity);
                _rentTracker[i] = new List<nint>(InitialQueueCapacity);
                _bufferCounts[i] = 0;

                for (int j = 0; j < initialBuffersPerPool; j++)
                {
                    _pool[i].Enqueue(PrivateAllocateMemory(i));
                }
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                int freeCount = 0;

                while (_pool[i].TryDequeue(out nint pointer))
                {
                    NativeMemory.Free((byte*)pointer);
                    freeCount++;
                }

                Debug.Assert(freeCount == _bufferCounts[i]);
                Debug.Assert(_rentTracker[i].Count == 0);
            }
        }

        public void EnsureCapacity(int requestedSize, int requestedCapacity)
        {
            lock (_poolLock)
            {
                PrivateEnsurePoolCapacityLocked(requestedSize, out int poolIndex, out int memorySize);

                while (_pool[poolIndex].Count < requestedCapacity)
                {
                    _pool[poolIndex].Enqueue(PrivateAllocateMemory(poolIndex));
                }
            }
        }

        public IBuffer Rent(int requestedSize)
        {
            if (requestedSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedSize), "Must be greater than zero");
            }

            nint pointer;
            int memorySize;

            lock (_poolLock)
            {
                pointer = PrivateRentUnsafeLocked(requestedSize, out memorySize);
            }

            return new UnsafeBuffer(this, pointer, memorySize, requestedSize);
        }

        public nint RentUnsafe(int requestedSize)
        {
            lock (_poolLock)
            {
                return PrivateRentUnsafeLocked(requestedSize, out _);
            }
        }

        public void Return(IBuffer buffer)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (buffer is UnsafeBuffer unsafeBuffer)
            {
                Return(unsafeBuffer);
            }
            else
            {
                throw new ArgumentException("Attempted to return incompatible buffer-instance", nameof(buffer));
            }
        }

        public void Return(UnsafeBuffer buffer)
        {
            int absolutePoolIndex = BufferPoolUtils.SizeToPoolIndex(buffer.MemorySize);
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
                NativeMemory.Fill((void*)buffer.Pointer, (nuint)buffer.MemorySize, 0);
            }

            lock (_poolLock)
            {
                _rentTracker[relativePoolIndex].Remove(buffer.Pointer);
                _pool[relativePoolIndex].Enqueue(buffer.Pointer);
            }
        }

        private nint PrivateRentUnsafeLocked(int requestedSize, out int memorySize)
        {
            PrivateEnsurePoolCapacityLocked(requestedSize, out int relativePoolIndex, out memorySize);

            if (!_pool[relativePoolIndex].TryDequeue(out nint pointer))
            {
                pointer = PrivateAllocateMemory(relativePoolIndex);
            }

            _rentTracker[relativePoolIndex].Add(pointer);

            return pointer;
        }

        private void PrivateEnsurePoolCapacityLocked(int requestedSize, out int relativePoolIndex, out int memorySize)
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
            memorySize = BufferPoolUtils.PoolIndexToSize(absolutePoolIndex);

            if (_pool.Length <= relativePoolIndex)
            {
                int previousPoolLength = _pool.Length;

                Array.Resize(ref _pool, relativePoolIndex + 1);
                Array.Resize(ref _rentTracker, relativePoolIndex + 1);
                Array.Resize(ref _bufferCounts, relativePoolIndex + 1);

                for (int i = previousPoolLength; i < _pool.Length; i++)
                {
                    if (_pool[i] == null)
                    {
                        _pool[i] = new Queue<nint>(InitialQueueCapacity);
                        _rentTracker[i] = new List<nint>(InitialQueueCapacity);
                        _bufferCounts[i] = 0;
                    }
                }
            }
        }

        private nint PrivateAllocateMemory(int relativePoolIndex)
        {
            _bufferCounts[relativePoolIndex]++;
            return (nint)NativeMemory.Alloc((nuint)BufferPoolUtils.PoolIndexToSize(_minPoolIndex + relativePoolIndex));
        }

    }

}
