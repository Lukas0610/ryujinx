using NUnit.Framework;
using Ryujinx.Audio.Renderer.Common;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer
{
    class BehaviourParameterTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.That(Unsafe.SizeOf<BehaviourParameter>(), Is.EqualTo(0x10));
            Assert.That(Unsafe.SizeOf<BehaviourParameter.ErrorInfo>(), Is.EqualTo(0x10));
        }
    }
}
