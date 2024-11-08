using System.Runtime.InteropServices;

namespace ARMeilleure.Translation.PTC
{

    [StructLayout(LayoutKind.Sequential, Pack = 1/*, Size = 40*/)]
    record struct PtcFeatureInfo(ulong FeatureInfo0, ulong FeatureInfo1, ulong FeatureInfo2, ulong FeatureInfo3, ulong FeatureInfo4);

}
