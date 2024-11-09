using System;

namespace ARMeilleure.Translation.PTC
{
    [Flags]
    enum PtcCacheFlags : ulong
    {

        None = 0,

        /// <summary>
        /// PTC was generated with a sparse address table in use
        /// </summary>
        SparseAddressTable = 1 << 0,

    }
}
