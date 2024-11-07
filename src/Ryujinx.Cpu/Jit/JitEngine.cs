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
        public ICpuContext CreateCpuContext(TranslatorConfiguration translatorConfiguration, IMemoryManager memoryManager, bool for64Bit)
        {
            return new JitCpuContext(translatorConfiguration, _tickSource, memoryManager, for64Bit);
        }
    }
}
