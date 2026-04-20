using System.Security.Cryptography;
using System.Text;

namespace BHXH_Backend.Helpers
{
    public static class HashHelper
    {
        public static string ToSha256(string raw)
        {
            var normalized = NormalizeLookupKeyword(raw);
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(bytes);
        }

        public static string NormalizeLookupKeyword(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Trim();
        }
    }
}
