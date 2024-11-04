using NUnit.Framework;
using Ryujinx.HLE.HOS.Services.Time.TimeZone;
using System.Runtime.CompilerServices;

namespace Ryujinx.Tests.Time
{
    internal class TimeZoneRuleTests
    {
        class EffectInfoParameterTests
        {
            [Test]
            public void EnsureTypeSize()
            {
                Assert.That(Unsafe.SizeOf<TimeZoneRule>(), Is.EqualTo(0x4000));
            }
        }
    }
}
