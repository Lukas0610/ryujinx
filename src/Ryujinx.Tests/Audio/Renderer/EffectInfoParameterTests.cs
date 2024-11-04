using NUnit.Framework;
using Ryujinx.Audio.Renderer.Parameter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer
{
    class EffectInfoParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.That(Unsafe.SizeOf<EffectInParameterVersion1>(), Is.EqualTo(0xC0));
            Assert.That(Unsafe.SizeOf<EffectInParameterVersion2>(), Is.EqualTo(0xC0));
        }
    }
}
