using ARMeilleure.CodeGen;
using ARMeilleure.Memory;
using Ryujinx.Common;
using System;

namespace ARMeilleure.Translation.PTC
{

    interface IPtc : IPtcLoadState, IDisposable
    {

        PtcState State { get; }

        IPtcProfiler Profiler { get; }

        void BeginExecution();

        void Enable();

        void Disable();

        void Stop();

        void Initialize(string titleIdText, string buildIdHashText, string displayVersion, bool enabled, MemoryManagerType memoryMode);

        void LoadTranslations(Translator translator);

        void MakeTranslations(Translator translator);

        void WriteCompiledFunction(ulong address, ulong guestSize, Hash128 hash, bool highCq, CompiledFunction compiledFunc);

    }

}
