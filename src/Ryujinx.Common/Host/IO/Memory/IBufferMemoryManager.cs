using Ryujinx.Common.Host.IO.Stats;
using System.Collections.Generic;

namespace Ryujinx.Common.Host.IO.Memory
{

    public interface IBufferMemoryManager
    {

        /// <summary>
        /// Allocates and assignes buffer-memory for the given page
        /// </summary>
        /// <param name="page"></param>
        void AllocPage(BufferedFilePage page);

        /// <summary>
        /// Frees and unassigns the buffer-memory of the given page
        /// </summary>
        void FreePage(BufferedFilePage page);

        /// <summary>
        /// Create a dictionary about statistics tracked by the memory-manager
        /// </summary>
        IEnumerable<IHostIOStat> GetStats();

    }

}
