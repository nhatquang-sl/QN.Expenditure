namespace Application.Common.Extensions
{
    public static class DateTimeExtension
    {
        public static long ToUnixTimestampMilliseconds(this DateTime dateTime)
        {
            return (long)dateTime.Subtract(DateTime.UnixEpoch).TotalMilliseconds;
        }

        public static long ToUnixTimestampSeconds(this DateTime dateTime)
        {
            return (long)dateTime.Subtract(DateTime.UnixEpoch).TotalSeconds;
        }
    }
}
