using System.Security.Cryptography;
using BHXH_Backend.Data;
using BHXH_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Services
{
    public static class OtpPurpose
    {
        public const string Register = "register";
        public const string Login = "login";
        public const string Reset = "reset";
    }

    public class OtpService
    {
        private const int OtpLength = 6;
        private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(5);

        private readonly ApplicationDbContext _dbContext;
        private readonly EmailService _emailService;

        public OtpService(ApplicationDbContext dbContext, EmailService emailService)
        {
            _dbContext = dbContext;
            _emailService = emailService;
        }

        public async Task<OtpCode> CreateAndSendOtpAsync(
            string email,
            string purpose,
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                throw new InvalidOperationException("Email is required.");
            }

            await InvalidateUnusedOtpAsync(normalizedEmail, purpose, cancellationToken);

            var otpValue = GenerateOtpValue();
            var otpRecord = new OtpCode
            {
                UserId = userId,
                Email = normalizedEmail,
                OtpValue = otpValue,
                Purpose = purpose,
                ExpireTime = DateTime.UtcNow.Add(OtpTtl),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.OtpCodes.Add(otpRecord);
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                await _emailService.SendOtpAsync(normalizedEmail, otpValue, cancellationToken);
            }
            catch
            {
                _dbContext.OtpCodes.Remove(otpRecord);
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw;
            }

            return otpRecord;
        }

        public async Task<OtpCode?> VerifyOtpAsync(
            string email,
            string otpValue,
            string purpose,
            bool markAsUsed = true,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedOtp = (otpValue ?? string.Empty).Trim();
            if (normalizedOtp.Length != OtpLength || normalizedOtp.Any(ch => !char.IsDigit(ch)))
            {
                return null;
            }

            var latestUnusedOtp = await _dbContext.OtpCodes
                .Where(o =>
                    o.Email == normalizedEmail &&
                    o.Purpose == purpose &&
                    !o.IsUsed)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestUnusedOtp == null)
            {
                return null;
            }

            if (latestUnusedOtp.ExpireTime <= DateTime.UtcNow || latestUnusedOtp.OtpValue != normalizedOtp)
            {
                return null;
            }

            if (markAsUsed)
            {
                latestUnusedOtp.IsUsed = true;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return latestUnusedOtp;
        }

        public async Task InvalidateUnusedOtpAsync(
            string email,
            string purpose,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            var activeOtps = await _dbContext.OtpCodes
                .Where(o =>
                    o.Email == normalizedEmail &&
                    o.Purpose == purpose &&
                    !o.IsUsed)
                .ToListAsync(cancellationToken);

            if (activeOtps.Count == 0)
            {
                return;
            }

            foreach (var otp in activeOtps)
            {
                otp.IsUsed = true;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private static string GenerateOtpValue()
        {
            var maxExclusive = (int)Math.Pow(10, OtpLength);
            var randomValue = RandomNumberGenerator.GetInt32(0, maxExclusive);
            return randomValue.ToString($"D{OtpLength}");
        }
    }
}
