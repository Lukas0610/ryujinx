using ARMeilleure.Memory;
using ARMeilleure.Translation;

namespace Ryujinx.Cpu.Jit
{
    public class JitEngine : ICpuEngine
    {
        private readonly ITickSource _tickSource;

        public JitEngine(ITickSource tickSource)
        {
            _tickSource = tickSource;
        }

        /// <inheritdoc/>
        public ICpuContext CreateCpuContext(CpuContextConfiguration cpuContextConfiguration, IMemoryManager memoryManager, bool for64Bit)
        {
            return new JitCpuContext(cpuContextConfiguration, _tickSource, memoryManager, for64Bit);
        }
    }
}
