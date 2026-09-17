namespace NovaWallet.Api.Infrastructure.Time;

public static class WatDay
{
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) Window(DateTimeOffset now)
    {
        var offset = TimeSpan.FromHours(1);
        var start = new DateTimeOffset(now.ToOffset(offset).Date, offset).ToUniversalTime();
        return (start, start.AddDays(1));
    }
    public static bool WithinLimit(long used, long amount, long limit) =>
        used >= 0 && limit > 0 && used <= limit && amount > 0 && amount <= limit - used;
}
