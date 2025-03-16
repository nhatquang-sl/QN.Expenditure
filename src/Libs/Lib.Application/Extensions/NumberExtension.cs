using System.Globalization;

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
        public static decimal FixedNumber(this decimal value, int fixedPlaces = 4)
        {
            var powFixed = (long)Math.Pow(10, fixedPlaces - 1);
            var pow = (long)Math.Pow(10, fixedPlaces);
            var adjustedValue = value * pow;

            while (adjustedValue < powFixed)
            {
                pow *= 10;
                adjustedValue = value * pow;
            }

            return Math.Floor(adjustedValue) / pow;
        }

        public static string FormatPrice(this decimal price)
        {
            var formatted = price.ToString("G29", CultureInfo.InvariantCulture);
            if (formatted.Contains('E'))
            {
                formatted = price.ToString("F10").TrimEnd('0').TrimEnd('.');
            }

            return formatted;
        }
    }
}