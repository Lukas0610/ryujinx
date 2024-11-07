using System;

namespace Ryujinx.Common.Host
{

    public sealed class HostFileSystemRequestProgressEventArgs : EventArgs
    {

        public string Path { get; }

        public long Current { get; }

        public long Total { get; }

        public double Speed { get; }

        internal HostFileSystemRequestProgressEventArgs(string path, long current, long total, double speed)
        {
            Path = path;
            Current = current;
            Total = total;
            Speed = speed;
        }

    }

}
