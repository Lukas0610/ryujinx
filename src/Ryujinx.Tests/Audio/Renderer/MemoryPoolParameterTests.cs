using NUnit.Framework;
using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer
{
    class MemoryPoolParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.That(Unsafe.SizeOf<MemoryPoolInParameter>(), Is.EqualTo(0x20));
            Assert.That(Unsafe.SizeOf<MemoryPoolOutStatus>(), Is.EqualTo(0x10));
        }
    }
}
