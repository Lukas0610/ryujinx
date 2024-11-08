using ARMeilleure.State;
using System;

namespace ARMeilleure.Translation.PTC
{

    interface IPtcProfiler : IDisposable
    {

        ulong StaticCodeStart { get; set; }

        ulong StaticCodeSize { get; set; }

        bool Enabled { get; }

        void Start();

        void Stop();

        void AddEntry(ulong address, ExecutionMode mode, bool highCq);

        void UpdateEntry(ulong address, ExecutionMode mode, bool highCq);

        void PerformSave();

    }

}
