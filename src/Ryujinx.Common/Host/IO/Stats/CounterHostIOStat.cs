namespace Ryujinx.Common.Host.IO.Stats
{

    public sealed class CounterHostIOStat : IHostIOStat
    {

        public string Name { get; }

        public long Value { get; }

        public CounterHostIOStat(string name, long value)
        {
            Name = name;
            Value = value;
        }

        public IHostIOStat Add(long value)
        {
            return new CounterHostIOStat(Name, Value + value);
        }

        public string GetFormattedValue()
        {
            return Value.ToString();
        }

    }

}
