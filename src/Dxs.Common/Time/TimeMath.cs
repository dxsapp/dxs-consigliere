namespace Dxs.Common.Time;

public static class TimeMath
{
    public static TimeSpan RoundDownTo(this TimeSpan timeSpan, TimeSpan to) =>
        TimeSpan.FromTicks((long)(Math.Floor((double)(timeSpan.Ticks + 1) / to.Ticks) * to.Ticks)); //+1 cause if

    public static TimeSpan RoundUpTo(this TimeSpan timeSpan, TimeSpan to) =>
        TimeSpan.FromTicks((long)(Math.Ceiling((double)timeSpan.Ticks / to.Ticks) * to.Ticks));

    public static DateTime RoundTo(this DateTime dateTime, TimeSpan to) =>
        DateTime.MinValue + (dateTime - DateTime.MinValue).RoundTo(to);

    public static DateTime RoundDownTo(this DateTime dateTime, TimeSpan to) =>
        DateTime.MinValue + (dateTime - DateTime.MinValue).RoundDownTo(to);

    public static DateTime RoundUpTo(this DateTime dateTime, TimeSpan to) =>
        DateTime.MinValue + (dateTime - DateTime.MinValue).RoundUpTo(to);

    public static TimeSpan RoundTo(this TimeSpan timeSpan, TimeSpan to) =>
        TimeSpan.FromTicks((long)(Math.Round((double)timeSpan.Ticks / to.Ticks) * to.Ticks));

    public static TimeSpan Abs(this TimeSpan timeSpan) => timeSpan.Ticks < 0 ? -timeSpan : timeSpan;

    public static TimeSpan Min(TimeSpan timeSpan1, TimeSpan timeSpan2) =>
        timeSpan1 < timeSpan2 ? timeSpan1 : timeSpan2;

    public static TimeSpan Min(TimeSpan timeSpan1, TimeSpan timeSpan2, TimeSpan timeSpan3) =>
        Min(timeSpan1, Min(timeSpan2, timeSpan3));

    public static TimeSpan Max(TimeSpan timeSpan1, TimeSpan timeSpan2) =>
        timeSpan1 > timeSpan2 ? timeSpan1 : timeSpan2;

    public static DateTime Min(DateTime dateTime1, DateTime dateTime2) =>
        dateTime1 < dateTime2 ? dateTime1 : dateTime2;

    public static DateTime Min(DateTime dateTime1, DateTime? dateTime2) =>
        dateTime2 is {} dateTime ? Min(dateTime1, dateTime) : dateTime1;

    public static DateTime Max(DateTime dateTime1, DateTime dateTime2) =>
        dateTime1 > dateTime2 ? dateTime1 : dateTime2;

    public static DateTime Min(IEnumerable<DateTime> dateTimes) =>
        new DateTime(dateTimes.Select(d => d.Ticks).Min());

    public static DateTime Max(IEnumerable<DateTime> dateTimes) =>
        new DateTime(dateTimes.Select(d => d.Ticks).Max());

    public static DateTime Average(DateTime dateTime1, DateTime dateTime2) => new DateTime((dateTime1.Ticks + dateTime2.Ticks) / 2);

    public static double Div(TimeSpan timeSpan1, TimeSpan timeSpan2) => (double)timeSpan1.Ticks / timeSpan2.Ticks;

    public static DateTime SubDays(this DateTime dateTime, int days) => dateTime.AddDays(-days);

    public static DateTime FromUnixSeconds(long timestamp) => DateTime.UnixEpoch.AddSeconds(timestamp);

    public static DateTime FromUnixMilliseconds(long timestamp) => DateTime.UnixEpoch.AddMilliseconds(timestamp);

    public static DateTime FromUnix(long timestamp) => IsUnixSeconds(timestamp) ? FromUnixSeconds(timestamp) : FromUnixMilliseconds(timestamp);
    public static long ToUnixTimestamp(this DateTime dateTime, TimeUnit unit) => (long)Math.Round((dateTime - DateTime.UnixEpoch).ToValue(unit));

    public static long ToUnixSeconds(this DateTime dateTime) => dateTime.ToUnixTimestamp(TimeUnit.Second);
    public static long ToUnixMilliseconds(this DateTime dateTime) => dateTime.ToUnixTimestamp(TimeUnit.Millisecond);

    public static TimeSpan FromUnixTimestamp(double value, TimeUnit unit) => unit switch
    {
        TimeUnit.Millisecond => TimeSpan.FromMilliseconds(value),
        TimeUnit.Second => TimeSpan.FromSeconds(value),
        TimeUnit.Minute => TimeSpan.FromMinutes(value),
        TimeUnit.Hour => TimeSpan.FromHours(value),
        TimeUnit.Day => TimeSpan.FromDays(value),
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };

    public static TimeSpan FromUnixTimestamp(long value, TimeUnit unit) =>
        FromUnixTimestamp((double)value, unit);

    public static bool IsUnixSeconds(long timestamp)
    {
        const long maxUnixTimeSec = 99999999999;
        return timestamp <= maxUnixTimeSec;
    }
}