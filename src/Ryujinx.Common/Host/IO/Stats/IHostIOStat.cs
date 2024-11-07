namespace Ryujinx.Common.Host.IO.Stats
{

    public interface IHostIOStat
    {

        string Name { get; }

        long Value { get; }

        IHostIOStat Add(long value);

        string GetFormattedValue();

    }

}
