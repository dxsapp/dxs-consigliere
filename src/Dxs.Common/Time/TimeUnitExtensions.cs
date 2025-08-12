namespace Dxs.Common.Time;

public static class TimeUnitExtensions
{
    public static double ToValue(this TimeSpan timeSpan, TimeUnit unit) => unit switch
    {
        TimeUnit.Millisecond => timeSpan.TotalMilliseconds,
        TimeUnit.Second => timeSpan.TotalSeconds,
        TimeUnit.Minute => timeSpan.TotalMinutes,
        TimeUnit.Hour => timeSpan.TotalHours,
        TimeUnit.Day => timeSpan.TotalDays,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };
        
    public static DateTime FromNow(this TimeSpan timeSpan)
        => DateTime.UtcNow.Add(timeSpan);
}