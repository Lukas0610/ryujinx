using Ryujinx.IO.Host.Buffer.Memory;
using System;

namespace Ryujinx.IO.Host.Buffer
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
