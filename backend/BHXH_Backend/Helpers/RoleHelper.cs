namespace BHXH_Backend.Helpers
{
    public static class RoleHelper
    {
        public const string User = "User";
        public const string Employee = "Employee";
        public const string Security = "Security";
        public const string Admin = "Admin";

        public static string Normalize(string? rawRole)
        {
            var role = (rawRole ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(role))
            {
                return User;
            }

            return role.ToLowerInvariant() switch
            {
                "staff" => Employee,
                "employee" => Employee,
                "soc" => Security,
                "security" => Security,
                "admin" => Admin,
                _ => User
            };
        }

        public static bool IsSupportedRole(string? role)
        {
            var normalized = Normalize(role);
            return normalized is User or Employee or Security or Admin;
        }

        public static string GetDashboardPath(string role)
        {
            return Normalize(role) switch
            {
                Employee => "/employee/dashboard",
                Security => "/security/dashboard",
                Admin => "/admin",
                _ => "/user/dashboard"
            };
        }
    }
}
