namespace Lib.Application.Extensions
{
    public static class LongExtension
    {
        public static DateTime ToDateTimeFromMilliseconds(this long milliseconds)
        {
            return DateTime.UnixEpoch.AddMilliseconds(milliseconds);
        }

        public static DateTime ToDateTimeFromSeconds(this long seconds)
        {
            return DateTime.UnixEpoch.AddSeconds(seconds);
        }
    }

    public static class DecimalExtension
    {
        public static decimal RoundDownStd(this decimal value)
        {
            return Math.Floor(value * 10000) / 10000;
        }
    }
}