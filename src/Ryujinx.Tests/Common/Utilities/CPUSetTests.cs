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

            Assert.AreEqual("0-7", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
        }

        [Test]
        public void TestFormattingEmptyMask()
        {
            var cpuSet = new CPUSet(0b0);

            Assert.AreEqual("", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.Length == 0);
        }

        [Test]
        public void TestFormattingMixedMask()
        {
            var cpuSet = new CPUSet(0b110001010010111);

            Assert.AreEqual("0-2,4,7,9,13-14", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 4, 7, 9, 13, 14 }));
        }

        [Test]
        public void TestParsingAndFormattingFullMask()
        {
            Assert.IsTrue(CPUSet.TryParse("0-7", out var cpuSet));

            Assert.AreEqual((BigInteger)0b11111111, cpuSet.Mask);
            Assert.AreEqual("0-7", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
        }

        [Test]
        public void TestParsingAndFormattingMixedMask()
        {
            Assert.IsTrue(CPUSet.TryParse("0-2,4,7,9,13-14", out var cpuSet));

            Assert.AreEqual((BigInteger)0b110001010010111, cpuSet.Mask);
            Assert.AreEqual("0-2,4,7,9,13-14", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 4, 7, 9, 13, 14 }));
        }

        [Test]
        public void TestParsingAndFormattingMixedMaskWithoutRanges()
        {
            Assert.IsTrue(CPUSet.TryParse("0,1,2,6,8,12,13,14", out var cpuSet));

            Assert.AreEqual((BigInteger)0b111000101000111, cpuSet.Mask);
            Assert.AreEqual("0-2,6,8,12-14", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 6, 8, 12, 13, 14 }));
        }

        [Test]
        public void TestParsingAndFormattingFullBinaryMask()
        {
            Assert.IsTrue(CPUSet.TryParse("0b11111111", out var cpuSet));

            Assert.AreEqual((BigInteger)0b11111111, cpuSet.Mask);
            Assert.AreEqual("0-7", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
        }

        [Test]
        public void TestParsingAndFormattingEmptyBinaryMask()
        {
            Assert.IsTrue(CPUSet.TryParse("0b0", out var cpuSet));

            Assert.AreEqual((BigInteger)0, cpuSet.Mask);
            Assert.AreEqual("", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.Length == 0);
        }

        [Test]
        public void TestParsingAndFormattingMixedBinaryMask()
        {
            Assert.IsTrue(CPUSet.TryParse("0b110001010010111", out var cpuSet));

            Assert.AreEqual((BigInteger)0b110001010010111, cpuSet.Mask);
            Assert.AreEqual("0-2,4,7,9,13-14", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 4, 7, 9, 13, 14 }));
        }

        [Test]
        public void TestParsingAndFormattingFullHexMask()
        {
            Assert.IsTrue(CPUSet.TryParse("0xff", out var cpuSet));

            Assert.AreEqual((BigInteger)0b11111111, cpuSet.Mask);
            Assert.AreEqual("0-7", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
        }

        [Test]
        public void TestParsingAndFormattingEmptyHexMask()
        {
            Assert.IsTrue(CPUSet.TryParse("0x0", out var cpuSet));

            Assert.AreEqual((BigInteger)0, cpuSet.Mask);
            Assert.AreEqual("", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.Length == 0);
        }

        [Test]
        public void TestParsingAndFormattingMixedHexMask()
        {
            Assert.IsTrue(CPUSet.TryParse("0x6297", out var cpuSet));

            Assert.AreEqual((BigInteger)0b110001010010111, cpuSet.Mask);
            Assert.AreEqual("0-2,4,7,9,13-14", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 4, 7, 9, 13, 14 }));
        }

        [Test]
        public void TestParsingAndFormattingFullHexMaskWithAltPrefix()
        {
            Assert.IsTrue(CPUSet.TryParse("&hff", out var cpuSet));

            Assert.AreEqual((BigInteger)0b11111111, cpuSet.Mask);
            Assert.AreEqual("0-7", cpuSet.String);
            Assert.IsTrue(cpuSet.Cores.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
        }

        [Test]
        public void TestParsingInvalidBinaryMask()
        {
            Assert.IsFalse(CPUSet.TryParse("0b00001110002", out _));
        }

        [Test]
        public void TestParsingInvalidHexMask()
        {
            Assert.IsFalse(CPUSet.TryParse("0xff00Z00ff", out _));
        }

        [Test]
        public void TestParsingInvalidHexMaskWithAltPrefix()
        {
            Assert.IsFalse(CPUSet.TryParse("&hff00Z00ff", out _));
        }

        [Test]
        public void TestParsingInvalidMaskWithLeadingComma()
        {
            Assert.IsFalse(CPUSet.TryParse(",0,1,2", out _));
        }

        [Test]
        public void TestParsingInvalidMaskWithTrailingComma()
        {
            Assert.IsFalse(CPUSet.TryParse("0,1,2,", out _));
        }

        [Test]
        public void TestParsingInvalidMaskWithLeadingAndTrailingComma()
        {
            Assert.IsFalse(CPUSet.TryParse(",0,1,2,", out _));
        }

        [Test]
        public void TestParsingInvalidMaskWithLeadingDash()
        {
            Assert.IsFalse(CPUSet.TryParse("-0,1,2", out _));
        }

        [Test]
        public void TestParsingInvalidMaskWithTrailingDash()
        {
            Assert.IsFalse(CPUSet.TryParse("0,1,2-", out _));
        }

        [Test]
        public void TestParsingInvalidMaskWithLeadingAndTrailingDash()
        {
            Assert.IsFalse(CPUSet.TryParse("-0,1,2-", out _));
        }

        [Test]
        public void TestParsingInvalidMaskWithInnerDoubleComma()
        {
            Assert.IsFalse(CPUSet.TryParse("0,1,,2", out _));
        }

        [Test]
        public void TestParsingInvalidMaskWithDashBeforeComma()
        {
            Assert.IsFalse(CPUSet.TryParse("0,1-,2", out _));
        }

    }
}
