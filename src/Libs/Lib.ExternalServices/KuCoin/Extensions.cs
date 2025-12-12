using System.Security.Cryptography;
using System.Text;

namespace Lib.ExternalServices.KuCoin
{
    public static class Extensions
    {
        private static readonly char[] Chars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-"
                .ToCharArray();

        public static string ToKcSymbol(this string value)
        {
            return value.Replace("-", "").Replace("USDT", "-USDT");
        }

        public static string ToNormalSymbol(this string value)
        {
            return value.Replace("-", "");
        }

        public static (string, string) GenerateSignature(this string apiSecret, string method, string endpoint,
            string body = "")
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var preHash = $"{timestamp}{method.ToUpper()}{endpoint}{body}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
            return (Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(preHash))), timestamp);
        }

        public static string ClientOid(this string symbol)
        {
            var orderId = $"{symbol.ToNormalSymbol()}{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            var randomLength = 40 - orderId.Length;
            return $"{orderId}{GetRandomString(randomLength)}";
        }

        private static string GetRandomString(int length)
        {
            var data = new byte[length];
            // Fill with cryptographically strong random bytes
            RandomNumberGenerator.Fill(data);

            var result = new char[length];
            for (var i = 0; i < length; i++)
            {
                // Map each byte to one of the allowed chars
                result[i] = Chars[data[i] % Chars.Length];
            }

            return new string(result);
        }
    }
}