using System;

namespace Ryujinx.Media
{

    public sealed class FFmpegException : Exception
    {

        public FFmpegException() { }

        public FFmpegException(string message)
            : base(message)
        { }

        public FFmpegException(string message, Exception innerException)
            : base(message, innerException)
        { }

        public FFmpegException(Exception innerException)
            : base(null, innerException)
        { }

        public static FFmpegException OutOfMemory()
        {
            return new FFmpegException(new OutOfMemoryException());
        }

    }

}
