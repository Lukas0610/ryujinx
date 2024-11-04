using NUnit.Framework;
using Ryujinx.Audio.Renderer.Server.Splitter;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    class SplitterDestinationTests
    {
        [Test]
        public void EnsureTypeSize()
        {
            Assert.That(Unsafe.SizeOf<SplitterDestinationVersion1>(), Is.EqualTo(0xE0));
            Assert.That(Unsafe.SizeOf<SplitterDestinationVersion2>(), Is.EqualTo(0x110));
        }
    }
}
