using System;
using System.Net.NetworkInformation;

namespace Ryujinx.Headless.SDL2
{
    class StatusUpdatedEventArgs : EventArgs
    {
        public bool VSyncEnabled;
        public string DockedMode;
        public string AspectRatio;
        public string GameStatus;
        public string GpuStatus;
        public string HostIoCacheStatus;

        public StatusUpdatedEventArgs(bool vSyncEnabled, string dockedMode, string aspectRatio, string gameStatus, string gpuStatus, string hostIoCacheStatus)
        {
            VSyncEnabled = vSyncEnabled;
            DockedMode = dockedMode;
            AspectRatio = aspectRatio;
            GameStatus = gameStatus;
            GpuStatus = gpuStatus;
            HostIoCacheStatus = hostIoCacheStatus;
        }
    }
}
