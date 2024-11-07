using System;
using System.Globalization;

namespace Ryujinx.Common.Utilities
{

    public static class ReadableStringUtils
    {

        private static readonly string[] Units = new[]
        {
            "Bytes",
            "KiB",
            "MiB",
            "GiB",
            "TiB",
        };

        private static readonly string[] ShortUnits = new[]
        {
            "B",
            "KB",
            "MB",
            "GB",
            "TB",
        };

        private const double SizeUnitOverflowPoint = .9;

        public static readonly CultureInfo English = new("en-US");

        public static string FormatSize(double value, int precision = 3, IFormatProvider provider = null, bool shortUnits = false)
        {
            string[] units = shortUnits ? ShortUnits : Units;
            int unitIndex = 0;

            while ((value / 1024.0) >= SizeUnitOverflowPoint && unitIndex < units.Length)
            {
                value /= 1024.0;
                unitIndex++;
            }

            return $"{Math.Round(value, precision).ToString(provider)} {units[unitIndex]}";
        }

    }

}
