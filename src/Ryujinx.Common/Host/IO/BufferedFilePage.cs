using System.Drawing;
using System.Threading;

namespace Ryujinx.Common.Host.IO
{

    public sealed class BufferedFilePage
    {

        private static ulong _globalIdentifierCounter;

        private readonly BufferedFile _file;
        private readonly SemaphoreSlim _evictionLock;

        private SemaphoreSlim _lock;

        /// <summary>
        /// The unmanaged memory area
        /// </summary>
        public unsafe byte* Memory = null;

        /// <summary>
        /// Unique global identifier of the page
        /// </summary>
        public ulong GlobalIdentifier;

        /// <summary>
        /// Size of the page
        /// </summary>
        public int Size;

        internal BufferedFilePage(BufferedFile file)
        {
            GlobalIdentifier = Interlocked.Increment(ref _globalIdentifierCounter);
            Size = file.PageSize;

            _file = file;
            _lock = new SemaphoreSlim(1, int.MaxValue);
            _evictionLock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Evists an active page from memory
        /// </summary>
        public void Evict()
        {
            _evictionLock.Wait();
            try
            {
                _file.MemoryManager.FreePage(this);

                // Reset the entry-lock to only allow a single entrant
                // (which is going allocate and read the page from the file again if required)
                _lock = new SemaphoreSlim(1, int.MaxValue);
            }
            finally
            {
                _evictionLock.Release();
            }
        }

        internal void AcquireLock()
        {
            _evictionLock.Wait();
            try
            {
                _lock.Wait();
            }
            finally
            {
                _evictionLock.Release();
            }
        }

        internal void UpgradeLock()
        {
            _lock.Release(int.MaxValue);
        }

        internal void ReleaseLock()
        {
            _lock.Release();
        }

    }

}
