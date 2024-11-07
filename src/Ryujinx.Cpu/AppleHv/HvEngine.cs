using ARMeilleure.Memory;
using ARMeilleure.Translation;
using System.Runtime.Versioning;

namespace Ryujinx.Cpu.AppleHv
{
    [SupportedOSPlatform("macos")]
    public class HvEngine : ICpuEngine
    {
        private readonly ITickSource _tickSource;

        public HvEngine(ITickSource tickSource)
        {
            _tickSource = tickSource;
        }

        /// <inheritdoc/>
        public ICpuContext CreateCpuContext(TranslatorConfiguration configuration, IMemoryManager memoryManager, bool for64Bit)
        {
            return new HvCpuContext(_tickSource, memoryManager, for64Bit);
        }
    }
}
