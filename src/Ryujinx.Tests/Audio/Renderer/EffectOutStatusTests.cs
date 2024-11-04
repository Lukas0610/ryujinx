using NUnit.Framework;
using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer
{
    class EffectOutStatusTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.That(Unsafe.SizeOf<EffectOutStatusVersion1>(), Is.EqualTo(0x10));
            Assert.That(Unsafe.SizeOf<EffectOutStatusVersion2>(), Is.EqualTo(0x90));
        }
    }
}
