using Ryujinx.Common.Utilities;

namespace Ryujinx.IO.Host.Stats
{

    public sealed class SizeHostIOStat : IHostIOStat
    {

        public string Name { get; }

        public long Value { get; }

        public SizeHostIOStat(string name, long value)
        {
            Name = name;
            Value = value;
        }

        public IHostIOStat Add(long value)
        {
            return new SizeHostIOStat(Name, Value + value);
        }

        public string GetFormattedValue()
        {
            return ReadableStringUtils.FormatSize(Value, 3, ReadableStringUtils.English);
        }

    }

}
