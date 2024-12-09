using System;

namespace Ryujinx.UI.Helpers
{

    public sealed class UIProgressEventArgs : EventArgs
    {

        public string Text { get; }

        public long Current { get; }

        public bool HasCurrent { get; }

        public long Total { get; }

        public bool HasTotal { get; }

        public double Speed { get; }

        public bool HasSpeed { get; }

        public UIProgressEventArgs(string text)
            : this(text, -1, false, -1, false, -1, false)
        { }

        public UIProgressEventArgs(string text, long current)
            : this(text, current, true, -1, false, -1, false)
        { }

        public UIProgressEventArgs(string text, long current, double speed)
            : this(text, current, true, -1, false, speed, true)
        { }

        public UIProgressEventArgs(string text, long current, long total, double speed)
            : this(text, current, true, total, true, speed, true)
        { }

        private UIProgressEventArgs(string text, long current, bool hasCurrent, long total, bool hasTotal, double speed, bool hasSpeed)
        {
            Text = text;
            Current = current;
            HasCurrent = hasCurrent;
            Total = total;
            HasTotal = hasTotal;
            Speed = speed;
            HasSpeed = hasSpeed;
        }

    }

}
