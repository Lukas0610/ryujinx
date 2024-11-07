using ARMeilleure.Memory;

namespace Ryujinx.Cpu.LightningJit
{
    public class LightningJitEngine : ICpuEngine
    {
        private readonly ITickSource _tickSource;

        public LightningJitEngine(ITickSource tickSource)
        {
            _tickSource = tickSource;
        }

        /// <inheritdoc/>
        public ICpuContext CreateCpuContext(CpuContextConfiguration cpuContextConfiguration, IMemoryManager memoryManager, bool for64Bit)
        {
            return new LightningJitCpuContext(cpuContextConfiguration, _tickSource, memoryManager, for64Bit);
        }
    }
}
