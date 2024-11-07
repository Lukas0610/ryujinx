using Ryujinx.Common.Host.IO.Memory;
using System;

namespace Ryujinx.Common.Host.IO
{

    public sealed class BufferedFileOptions
    {

        public IRefBufferMemoryManager MemoryManager { get; set; } = null;

        public int PageSize { get; set; } = Environment.SystemPageSize;

        public bool SequentialScan { get; set; } = false;

        public bool DisposeWithLastStream { get; set; } = false;

        public bool CloseFileStreamAfterPrefetching { get; set; } = false;

    }

}
