using System.Text.RegularExpressions;

namespace BHXH_Backend.Helpers
{
    public static class PasswordPolicyHelper
    {
        private static readonly Regex StrongPasswordRegex = new(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            RegexOptions.Compiled);

        public static bool IsStrong(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            return StrongPasswordRegex.IsMatch(password);
        }

        public static string PolicyDescription =>
            "Mat khau toi thieu 8 ky tu, gom chu hoa, chu thuong, so va ky tu dac biet (@$!%*?&).";
    }
}
