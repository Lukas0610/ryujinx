using System;

namespace Ryujinx.Ava.UI.Models
{
    internal class StatusInitEventArgs : EventArgs
    {
        public string GpuDriver { get; }
        public string GpuBackend { get; }

        public StatusInitEventArgs(string gpuDriver, string gpuBackend)
        {
            GpuDriver = gpuDriver;
            GpuBackend = gpuBackend;
        }
    }
}
