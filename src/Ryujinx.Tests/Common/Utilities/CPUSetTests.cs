using NUnit.Framework;
using Ryujinx.Common.Utilities;
using System.Linq;
using System.Numerics;

namespace Ryujinx.Tests.Common.Utilities
{
    internal class CPUSetTests
    {

        [Test]
        public void TestFormattingFullMask()
        {
            var cpuSet = new CPUSet(0b11111111);

            Assert.That(cpuSet.String, Is.EqualTo("0-7"));
            Assert.That(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }), Is.True);
        }

        [Test]
        public void TestFormattingEmptyMask()
        {
            var cpuSet = new CPUSet(0b0);

            Assert.That(cpuSet.String, Is.EqualTo(""));
            Assert.That(cpuSet.Cores.Length, Is.EqualTo(0));
        }

        [Test]
        public void TestFormattingMixedMask()
        {
            var cpuSet = new CPUSet(0b110001010010111);

            Assert.That(cpuSet.String, Is.EqualTo("0-2,4,7,9,13-14"));
            Assert.That(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 4, 7, 9, 13, 14 }), Is.True);
        }

        [Test]
        public void TestParsingAndFormattingFullMask()
        {
            Assert.That(CPUSet.TryParse("0-7", out var cpuSet), Is.True);

            Assert.That(cpuSet.Mask, Is.EqualTo((BigInteger)0b11111111));
            Assert.That(cpuSet.String, Is.EqualTo("0-7"));
            Assert.That(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }), Is.True);
        }

        [Test]
        public void TestParsingAndFormattingMixedMask()
        {
            Assert.That(CPUSet.TryParse("0-2,4,7,9,13-14", out var cpuSet), Is.True);

            Assert.That(cpuSet.Mask, Is.EqualTo((BigInteger)0b110001010010111));
            Assert.That(cpuSet.String, Is.EqualTo("0-2,4,7,9,13-14"));
            Assert.That(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 4, 7, 9, 13, 14 }), Is.True);
        }

        [Test]
        public void TestParsingAndFormattingMixedMaskWithoutRanges()
        {
            Assert.That(CPUSet.TryParse("0,1,2,6,8,12,13,14", out var cpuSet), Is.True);

            Assert.That(cpuSet.Mask, Is.EqualTo((BigInteger)0b111000101000111));
            Assert.That(cpuSet.String, Is.EqualTo("0-2,6,8,12-14"));
            Assert.That(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 6, 8, 12, 13, 14 }), Is.True);
        }

        [Test]
        public void TestParsingAndFormattingFullBinaryMask()
        {
            Assert.That(CPUSet.TryParse("0b11111111", out var cpuSet), Is.True);

            Assert.That(cpuSet.Mask, Is.EqualTo((BigInteger)0b11111111));
            Assert.That(cpuSet.String, Is.EqualTo("0-7"));
            Assert.That(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }), Is.True);
        }

        [Test]
        public void TestParsingAndFormattingEmptyBinaryMask()
        {
            Assert.That(CPUSet.TryParse("0b0", out var cpuSet), Is.True);

            Assert.That(cpuSet.Mask, Is.EqualTo((BigInteger)0));
            Assert.That(cpuSet.String, Is.EqualTo(""));
            Assert.That(cpuSet.Cores.Length, Is.EqualTo(0));
        }

        [Test]
        public void TestParsingAndFormattingMixedBinaryMask()
        {
            Assert.That(CPUSet.TryParse("0b110001010010111", out var cpuSet), Is.True);

            Assert.That(cpuSet.Mask, Is.EqualTo((BigInteger)0b110001010010111));
            Assert.That(cpuSet.String, Is.EqualTo("0-2,4,7,9,13-14"));
            Assert.That(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 4, 7, 9, 13, 14 }), Is.True);
        }

        [Test]
        public void TestParsingAndFormattingFullHexMask()
        {
            Assert.That(CPUSet.TryParse("0xff", out var cpuSet), Is.True);

            Assert.That(cpuSet.Mask, Is.EqualTo((BigInteger)0b11111111));
            Assert.That(cpuSet.String, Is.EqualTo("0-7"));
            Assert.That(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }), Is.True);
        }

        [Test]
        public void TestParsingAndFormattingEmptyHexMask()
        {
            Assert.That(CPUSet.TryParse("0x0", out var cpuSet), Is.True);

            Assert.That(cpuSet.Mask, Is.EqualTo((BigInteger)0));
            Assert.That(cpuSet.String, Is.EqualTo(""));
            Assert.That(cpuSet.Cores.Length, Is.EqualTo(0));
        }

        [Test]
        public void TestParsingAndFormattingMixedHexMask()
        {
            Assert.That(CPUSet.TryParse("0x6297", out var cpuSet), Is.True);

            Assert.That(cpuSet.Mask, Is.EqualTo((BigInteger)0b110001010010111));
            Assert.That(cpuSet.String, Is.EqualTo("0-2,4,7,9,13-14"));
            Assert.That(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 4, 7, 9, 13, 14 }), Is.True);
        }

        [Test]
        public void TestParsingAndFormattingFullHexMaskWithAltPrefix()
        {
            Assert.That(CPUSet.TryParse("&hff", out var cpuSet), Is.True);

            Assert.That(cpuSet.Mask, Is.EqualTo((BigInteger)0b11111111));
            Assert.That(cpuSet.String, Is.EqualTo("0-7"));
            Assert.That(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }), Is.True);
        }

        [Test]
        public void TestParsingInvalidBinaryMask()
        {
            Assert.That(CPUSet.TryParse("0b00001110002", out _), Is.False);
        }

        [Test]
        public void TestParsingInvalidHexMask()
        {
            Assert.That(CPUSet.TryParse("0xff00Z00ff", out _), Is.False);
        }

        [Test]
        public void TestParsingInvalidHexMaskWithAltPrefix()
        {
            Assert.That(CPUSet.TryParse("&hff00Z00ff", out _), Is.False);
        }

        [Test]
        public void TestParsingInvalidMaskWithLeadingComma()
        {
            Assert.That(CPUSet.TryParse(",0,1,2", out _), Is.False);
        }

        [Test]
        public void TestParsingInvalidMaskWithTrailingComma()
        {
            Assert.That(CPUSet.TryParse("0,1,2,", out _), Is.False);
        }

        [Test]
        public void TestParsingInvalidMaskWithLeadingAndTrailingComma()
        {
            Assert.That(CPUSet.TryParse(",0,1,2,", out _), Is.False);
        }

        [Test]
        public void TestParsingInvalidMaskWithLeadingDash()
        {
            Assert.That(CPUSet.TryParse("-0,1,2", out _), Is.False);
        }

        [Test]
        public void TestParsingInvalidMaskWithTrailingDash()
        {
            Assert.That(CPUSet.TryParse("0,1,2-", out _), Is.False);
        }

        [Test]
        public void TestParsingInvalidMaskWithLeadingAndTrailingDash()
        {
            Assert.That(CPUSet.TryParse("-0,1,2-", out _), Is.False);
        }

        [Test]
        public void TestParsingInvalidMaskWithInnerDoubleComma()
        {
            Assert.That(CPUSet.TryParse("0,1,,2", out _), Is.False);
        }

        [Test]
        public void TestParsingInvalidMaskWithDashBeforeComma()
        {
            Assert.That(CPUSet.TryParse("0,1-,2", out _), Is.False);
        }

    }
}
