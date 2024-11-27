using LibHac;
using Ryujinx.Audio.Integration;
using Ryujinx.Cpu;
using Ryujinx.Horizon.Sdk.Account;
using Ryujinx.Horizon.Sdk.Fs;
using Ryujinx.Media.Capture;

namespace Ryujinx.Horizon
{
    public readonly struct HorizonOptions
    {
        public bool IgnoreMissingServices { get; }
        public bool ThrowOnInvalidCommandIds { get; }

        public HorizonClient BcatClient { get; }
        public IFsClient FsClient { get; }
        public IEmulatorAccountManager AccountManager { get; }
        public IHardwareDeviceDriver AudioDeviceDriver { get; }
        public CaptureHandler CaptureHandler { get; }
        public ITickSource TickSource { get; }

        public HorizonOptions(
            bool ignoreMissingServices,
            HorizonClient bcatClient,
            IFsClient fsClient,
            IEmulatorAccountManager accountManager,
            IHardwareDeviceDriver audioDeviceDriver,
            CaptureHandler captureHandler,
            ITickSource tickSource)
        {
            IgnoreMissingServices = ignoreMissingServices;
            ThrowOnInvalidCommandIds = true;
            BcatClient = bcatClient;
            FsClient = fsClient;
            AccountManager = accountManager;
            AudioDeviceDriver = audioDeviceDriver;
            CaptureHandler = captureHandler;
            TickSource = tickSource;
        }
    }
}
