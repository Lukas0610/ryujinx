using Ryujinx.IO.Host.Stats;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.IO.Host.Buffer.Memory
{

    /// <summary>
    /// Buffer memory-manager providing per-page buffer memory allocation
    /// </summary>
    public abstract class PageBufferMemoryManager : IBufferMemoryManager
    {

        private long _counterAllocatedPages = 0;
        private long _counterAllocatedMemory = 0;

        public long NumberOfAllocatedPages
        {
            get => Interlocked.Read(ref _counterAllocatedPages);
        }

        public long SizeOfAllocatedMemory
        {
            get => Interlocked.Read(ref _counterAllocatedMemory);
        }

        /// <inheritdoc />
        public void AllocPage(BufferedFilePage page)
        {
            int pageSize = page.Size;

            unsafe
            {
                if (page.Memory != null)
                {
                    throw new System.InvalidOperationException("Cannot allocate memory for page which already has allocated memory assigned");
                }

                page.Memory = (byte*)NativeMemory.AllocZeroed((nuint)pageSize);
            }

            Interlocked.Increment(ref _counterAllocatedPages);
            Interlocked.Add(ref _counterAllocatedMemory, pageSize);
        }

        /// <inheritdoc />
        public void FreePage(BufferedFilePage page)
        {
            int pageSize = page.Size;

            unsafe
            {
                byte* memory = page.Memory;

                page.Memory = null;

                if (memory != null)
                {
                    NativeMemory.Free(memory);

                    Interlocked.Decrement(ref _counterAllocatedPages);
                    Interlocked.Add(ref _counterAllocatedMemory, -pageSize);
                }
            }
        }

        /// <inheritdoc />
        public virtual IEnumerable<IHostIOStat> GetStats()
        {
            yield return new CounterHostIOStat("NumberOfAllocatedPages", Interlocked.Read(ref _counterAllocatedPages));
            yield return new SizeHostIOStat("SizeOfAllocatedMemory", Interlocked.Read(ref _counterAllocatedMemory));
        }

    }

}
