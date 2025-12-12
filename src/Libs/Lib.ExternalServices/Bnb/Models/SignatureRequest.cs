using Refit;
using System.Security.Cryptography;
using System.Text;

namespace Lib.ExternalServices.Bnb.Models
{
    public class SignatureRequest
    {
        [AliasAs("signature")]
        public string Signature { get; set; }

        public string Sign(string input, string secretKey)
        {
            using HMACSHA256 hmac = new(Encoding.ASCII.GetBytes(secretKey));
            return BitConverter.ToString(hmac.ComputeHash(Encoding.ASCII.GetBytes(input))).Replace("-", "").ToLower();
        }
    }
}
