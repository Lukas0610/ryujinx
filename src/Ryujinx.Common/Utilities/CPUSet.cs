using System;
using System.Collections.Generic;
using System.Numerics;

namespace Ryujinx.Common.Utilities
{
    public sealed class CPUSet : IEquatable<CPUSet>
    {

        private const string CharDecTable = "0123456789";
        private const string CharHexTable = "0123456789abcdef";

        public BigInteger Mask { get; }

        public uint UIntMask { get; }

        public ulong ULongMask { get; }

        public string String { get; }

        public int[] Cores { get; }

        public CPUSet(BigInteger mask)
        {
            Mask = mask;
            UIntMask = (uint)mask;
            ULongMask = (ulong)mask;

            List<int> cores = new();
            List<string> stringSegments = new();

            int rangeStart = -1;
            bool hasRangeStart = false;

            // Make a final run to properly close open ranges at the end of the mask
            for (int core = 0; core <= (int)mask.GetBitLength(); core++)
            {
                BigInteger coreMask = BigInteger.One << core;

                if ((mask & coreMask) == coreMask)
                {
                    cores.Add(core);

                    if (!hasRangeStart)
                    {
                        rangeStart = core;
                        hasRangeStart = true;
                    }
                }
                else
                {
                    if (hasRangeStart && rangeStart >= 0)
                    {
                        if (rangeStart != (core - 1))
                        {
                            stringSegments.Add($"{rangeStart}-{core - 1}");
                        }
                        else
                        {
                            stringSegments.Add($"{rangeStart}");
                        }

                        rangeStart = -1;
                        hasRangeStart = false;
                    }
                }
            }

            String = string.Join(",", stringSegments);
            Cores = cores.ToArray();
        }

        public static CPUSet FromSingleCore(int core)
        {
            if (core < 0)
                throw new ArgumentOutOfRangeException(nameof(core));

            return new(BigInteger.One << core);
        }

        public static CPUSet Default()
        {
            BigInteger value = BigInteger.Zero;

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                value = (value << 1) | 1;
            }

            return new(value);
        }

        public static CPUSet Parse(string str)
        {
            if (!TryParseImpl(str, out CPUSet cpuSet, out int errorIndex))
            {
                if (errorIndex == str.Length)
                {
                    throw new FormatException($"Could not parse \"{str}\" as CPU-Set: Unexpected end of string");
                }
                else
                {
                    throw new FormatException($"Could not parse \"{str}\" as CPU-Set: Unexpected value at position {errorIndex}/index {errorIndex - 1}");
                }
            }

            return cpuSet;
        }

        public static CPUSet ParseOrDefault(string str)
        {
            if (TryParseImpl(str, out CPUSet cpuSet, out _))
                return cpuSet;

            return Default();
        }

        public static bool TryParse(string str, out CPUSet cpuSet)
        {
            return TryParseImpl(str, out cpuSet, out _);
        }

        public override string ToString()
        {
            return String;
        }

        public override bool Equals(object obj)
        {
            return (obj is CPUSet otherCpuSet) && Equals(otherCpuSet);
        }

        public bool Equals(CPUSet other)
        {
            return (other != null) && (Mask == other.Mask);
        }

        public override int GetHashCode()
        {
            return Mask.GetHashCode();
        }

        private static bool TryParseImpl(string str, out CPUSet cpuSet, out int errorIndex)
        {
            cpuSet = null;
            errorIndex = -1;

            BigInteger value = BigInteger.Zero;

            if (string.IsNullOrWhiteSpace(str) || str.Equals("*"))
            {
                for (int i = 0; i < Environment.ProcessorCount; i++)
                {
                    value = (value << 1) | 1;
                }
            }
            else if (str.StartsWith("0b"))
            {
                // Parse as bitmask
                for (int i = 2; i < str.Length; i++)
                {
                    char ch = str[i];

                    if (ch == '0')
                    {
                        value <<= 1;
                    }
                    else if (ch == '1')
                    {
                        value = (value << 1) | 1;
                    }
                    else
                    {
                        errorIndex = i;
                        return false;
                    }
                }
            }
            else if (str.StartsWith("0x") || str.StartsWith("&h"))
            {
                // Parse as hex
                for (int i = 2; i < str.Length; i++)
                {
                    char ch = char.ToLower(str[i]);
                    int charValue = CharHexTable.IndexOf(ch);

                    if (charValue >= 0 && charValue <= 15)
                    {
                        value = (value << 4) | charValue;
                    }
                    else
                    {
                        errorIndex = i;
                        return false;
                    }
                }
            }
            else
            {
                // Parse as cpuset-string
                int number = 0;
                bool hasNumber = false;
                int carry = 0;
                bool isRange = false;

                // Appending a comma makes the parser-code simpler
                str = $"{str},";

                for (int i = 0; i < str.Length; i++)
                {
                    char ch = str[i];

                    if (char.IsDigit(ch))
                    {
                        int charValue = ch - '0';

                        if (charValue >= 0 && charValue <= 9)
                        {
                            number = (number * 10) + charValue;
                            hasNumber = true;
                        }
                        else
                        {
                            errorIndex = i;
                            return false;
                        }
                    }
                    else if (ch == ',')
                    {
                        if (!hasNumber)
                        {
                            errorIndex = i;
                            return false;
                        }

                        if (isRange)
                        {
                            for (int j = carry; j <= number; j++)
                            {
                                value |= BigInteger.One << j;
                            }
                        }
                        else
                        {
                            value |= BigInteger.One << number;
                        }

                        carry = 0;
                        number = 0;
                        isRange = false;
                        hasNumber = false;
                    }
                    else if (ch == '-')
                    {
                        if (!hasNumber)
                        {
                            errorIndex = i;
                            return false;
                        }

                        carry = number;
                        number = 0;
                        isRange = true;
                        hasNumber = false;
                    }
                    else
                    {
                        errorIndex = i;
                        return false;
                    }
                }

                if (hasNumber || isRange)
                {
                    errorIndex = str.Length;
                    return false;
                }
            }

            cpuSet = new(value);

            return true;
        }

    }
}
