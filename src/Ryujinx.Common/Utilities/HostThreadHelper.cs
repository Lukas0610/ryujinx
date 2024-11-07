using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Ryujinx.Common.Utilities
{
    public static partial class HostThreadHelper
    {

        [DllImport("libc.so.6")]
        internal static extern int sched_setaffinity(int pid, IntPtr maskSize, ref ulong mask);

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool SetThreadGroupAffinity(IntPtr thread, ref GROUP_AFFINITY groupAffinity, out GROUP_AFFINITY previousGroupAffinity);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct GROUP_AFFINITY
        {
            public UIntPtr Mask;

            [MarshalAs(UnmanagedType.U2)]
            public ushort Group;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.U2)]
            public ushort[] Reserved;
        }

        public static void SetCurrentThreadAffinity(CPUSet cpuSet)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                IntPtr currThread = GetCurrentThread();

                GROUP_AFFINITY groupAffinity = new GROUP_AFFINITY
                {
                    Group = 0,
                    Mask = (UIntPtr)cpuSet.Mask
                };

                SetThreadGroupAffinity(currThread, ref groupAffinity, out GROUP_AFFINITY previousGroupAffinity);
            }
            else if (OperatingSystem.IsLinux())
            {
                ulong mask = cpuSet.ULongMask;

                sched_setaffinity(0, 8, ref mask);
            }
        }

    }
}
