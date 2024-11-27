using System;

namespace Ryujinx.Common.Buffers
{

    public interface IBuffer : IDisposable
    {

        int Length { get; }

        int MemorySize { get; }

        ReadOnlySpan<byte> ReadOnlySpan { get; }

        Span<byte> Span { get; }

        /// <summary>
        /// Retrieve the fixed pointer of the buffer memory
        /// </summary>
        void Fixed(Action<nint> callback);

        /// <summary>
        /// Retrieve the fixed pointer and dispose (return) the buffer to the parent pool 
        /// </summary>
        public unsafe void ConsumeFixed(Action<nint> callback)
        {
            try
            {
                Fixed(callback);
            }
            finally
            {
                Dispose();
            }
        }

    }

}
