using System;

namespace Ryujinx.IO
{

    public sealed class IOProgressChangedEventArgs : EventArgs
    {

        public string Path { get; }

        public long Current { get; }

        public long Total { get; }

        public double Speed { get; }

        internal IOProgressChangedEventArgs(string path, long current, long total, double speed)
        {
            Path = path;
            Current = current;
            Total = total;
            Speed = speed;
        }

    }

}
