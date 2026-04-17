namespace BHXH_Backend.Helpers
{
    public static class ConfigurationHelper
    {
        public static string? GetAesKey(IConfiguration configuration)
        {
            return Environment.GetEnvironmentVariable("AesSettings__Key")
                ?? Environment.GetEnvironmentVariable("AES_KEY")
                ?? configuration["AesSettings:Key"];
        }

        public static string? GetJwtSecret(IConfiguration configuration)
        {
            return Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? Environment.GetEnvironmentVariable("JwtSettings__SecretKey")
                ?? configuration["JwtSettings:SecretKey"];
        }
    }
}
