using ARMeilleure.Common;
using ARMeilleure.Memory;
using ARMeilleure.Translation;
using Ryujinx.Cpu.Signal;
using Ryujinx.Memory;

namespace Ryujinx.Cpu.Jit
{
    class JitCpuContext : ICpuContext
    {
        private readonly ITickSource _tickSource;
        private readonly Translator _translator;
        private readonly AddressTable<ulong> _functionTable;

        /// <inheritdoc/>
        public bool HasSparseAddressTable
        {
            get => _functionTable.Sparse;
        }

        public JitCpuContext(CpuContextConfiguration cpuContextConfiguration, ITickSource tickSource, IMemoryManager memory, bool for64Bit)
        {
            _tickSource = tickSource;
            _functionTable = AddressTable<ulong>.CreateForArm(for64Bit, memory.Type, cpuContextConfiguration.UseSparseAddressTable);

            _translator = new Translator(cpuContextConfiguration.TranslatorConfiguration,
                                         new JitMemoryAllocator(forJit: true),
                                         memory,
                                         _functionTable);

            if (memory.Type.IsHostMappedOrTracked())
            {
                NativeSignalHandler.InitializeSignalHandler();
            }

            memory.UnmapEvent += UnmapHandler;
        }

        private void UnmapHandler(ulong address, ulong size)
        {
            _translator.InvalidateJitCacheRegion(address, size);
        }

        /// <inheritdoc/>
        public IExecutionContext CreateExecutionContext(ExceptionCallbacks exceptionCallbacks)
        {
            return new JitExecutionContext(new JitMemoryAllocator(), _tickSource, exceptionCallbacks);
        }

        /// <inheritdoc/>
        public void Execute(IExecutionContext context, ulong address)
        {
            _translator.Execute(((JitExecutionContext)context).Impl, address);
        }

        /// <inheritdoc/>
        public void InvalidateCacheRegion(ulong address, ulong size)
        {
            _translator.InvalidateJitCacheRegion(address, size);
        }

        /// <inheritdoc/>
        public IDiskCacheLoadState LoadDiskCache(string titleIdText, string buildIdHashText, string displayVersion, bool enabled)
        {
            return new JitDiskCacheLoadState(_translator.LoadDiskCache(titleIdText, buildIdHashText, displayVersion, enabled));
        }

        /// <inheritdoc/>
        public void PrepareCodeRange(ulong address, ulong size)
        {
            _functionTable.SignalCodeRange(address, size);
            _translator.PrepareCodeRange(address, size);
        }

        public void Dispose()
        {
        }
    }
}
