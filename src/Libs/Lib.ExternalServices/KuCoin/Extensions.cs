namespace Lib.ExternalServices.KuCoin
{
    public static class Extensions
    {
        public static string ToKcSymbol(this string value)
        {
            return value.Replace("-", "").Replace("USDT", "-USDT");
        }

        public static string ToNormalSymbol(this string value)
        {
            return value.Replace("-", "");
        }
    }
}