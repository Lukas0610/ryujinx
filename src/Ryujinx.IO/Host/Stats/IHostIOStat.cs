namespace Ryujinx.IO.Host.Stats
{

    public interface IHostIOStat
    {

        string Name { get; }

        long Value { get; }

        IHostIOStat Add(long value);

        string GetFormattedValue();

    }

}
