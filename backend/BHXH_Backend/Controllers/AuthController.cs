using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using BHXH_Backend.Data;
using BHXH_Backend.Dtos;
using BHXH_Backend.Helpers;
using BHXH_Backend.Models;
using BHXH_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 30;

        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logService;
        private readonly OtpService _otpService;
        private readonly IConfiguration _configuration;

        public AuthController(
            ApplicationDbContext context,
            SystemLogService logService,
            OtpService otpService,
            IConfiguration configuration)
        {
            _context = context;
            _logService = logService;
            _otpService = otpService;
            _configuration = configuration;
        }

        [HttpPost("register")]
        [HttpPost("/api/register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var ipAddress = GetClientIpAddress();
            var email = request.Email.Trim().ToLowerInvariant();
            var username = request.Username.Trim();
            var phoneNumber = request.PhoneNumber.Trim();
            var bhxhCode = (request.BhxhCode ?? string.Empty).Trim();

            if (!Regex.IsMatch(bhxhCode, "^\\d{10}$"))
            {
                return BadRequest(new { message = "Mã số BHXH phải gồm đúng 10 chữ số." });
            }

            if (!PasswordPolicyHelper.IsStrong(request.Password))
            {
                return BadRequest(new { message = PasswordPolicyHelper.PolicyDescription });
            }

            var userExists = await _context.Users.AnyAsync(u =>
                u.Email == email ||
                u.Username == username ||
                u.PhoneNumber == phoneNumber ||
                u.BhxhCode == bhxhCode);
            if (userExists)
            {
                return BadRequest(new { message = "Thông tin đăng ký đã tồn tại trong hệ thống." });
            }

            var pendingByOtherEmail = await _context.PendingRegistrations.AnyAsync(p =>
                p.Email != email &&
                (p.Username == username || p.PhoneNumber == phoneNumber || p.BhxhCode == bhxhCode));
            if (pendingByOtherEmail)
            {
                return BadRequest(new { message = "Thông tin đăng ký đang chờ xác thực ở email khác." });
            }

            var pendingRegistration = await _context.PendingRegistrations
                .FirstOrDefaultAsync(p => p.Email == email);

            if (pendingRegistration == null)
            {
                pendingRegistration = new PendingRegistration
                {
                    Email = email
                };
                _context.PendingRegistrations.Add(pendingRegistration);
            }

            pendingRegistration.Username = username;
            pendingRegistration.FullName = request.FullName.Trim();
            pendingRegistration.PhoneNumber = phoneNumber;
            pendingRegistration.BhxhCode = bhxhCode;
            pendingRegistration.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            pendingRegistration.CreatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            try
            {
                await _otpService.CreateAndSendOtpAsync(email, OtpPurpose.Register, cancellationToken: HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                await _logService.WriteLogAsync(username, "REGISTER_OTP_FAILED", $"Gửi OTP đăng ký thất bại: {ex.Message}", ipAddress);
                return StatusCode(500, new { message = "Không thể gửi OTP đăng ký. Vui lòng thử lại." });
            }

            await _logService.WriteLogAsync(username, "REGISTER_OTP_SENT", "Đã gửi OTP đăng ký qua email.", ipAddress);
            return Ok(new
            {
                message = "Đã gửi OTP đăng ký. Vui lòng kiểm tra email để xác thực.",
                requiresOtp = true,
                purpose = OtpPurpose.Register
            });
        }

        [HttpPost("verify-register-otp")]
        [HttpPost("/api/verify-register-otp")]
        public async Task<IActionResult> VerifyRegisterOtp([FromBody] VerifyRegisterOtpDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var ipAddress = GetClientIpAddress();
            var email = request.Email.Trim().ToLowerInvariant();

            var pendingRegistration = await _context.PendingRegistrations.FirstOrDefaultAsync(p => p.Email == email);
            if (pendingRegistration == null)
            {
                return BadRequest(new { message = "Không tìm thấy phiên đăng ký chờ xác thực." });
            }

            var otpRecord = await _otpService.VerifyOtpAsync(email, request.Otp, OtpPurpose.Register, true, HttpContext.RequestAborted);
            if (otpRecord == null)
            {
                return BadRequest(new { message = "OTP không hợp lệ, đã hết hạn hoặc đã được sử dụng." });
            }

            var userExists = await _context.Users.AnyAsync(u =>
                u.Email == pendingRegistration.Email ||
                u.Username == pendingRegistration.Username ||
                u.PhoneNumber == pendingRegistration.PhoneNumber ||
                u.BhxhCode == pendingRegistration.BhxhCode);
            if (userExists)
            {
                return BadRequest(new { message = "Thông tin tài khoản đã tồn tại, không thể tạo mới." });
            }

            var user = new User
            {
                Username = pendingRegistration.Username,
                PasswordHash = pendingRegistration.PasswordHash,
                FullName = pendingRegistration.FullName,
                PhoneNumber = pendingRegistration.PhoneNumber,
                Email = pendingRegistration.Email,
                Role = RoleHelper.User,
                FailedLoginAttempts = 0,
                BhxhCode = pendingRegistration.BhxhCode
            };

            _context.Users.Add(user);
            _context.PendingRegistrations.Remove(pendingRegistration);
            await _context.SaveChangesAsync();

            await _logService.WriteLogAsync(user.Username, "REGISTER_SUCCESS", "Đăng ký thành công sau khi xác thực OTP.", ipAddress);
            return Ok(new
            {
                message = "Đăng ký thành công.",
                createdUserId = user.Id
            });
        }

        [HttpPost("login")]
        [HttpPost("/api/login")]
        public async Task<IActionResult> Login([FromBody] LoginDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var ipAddress = GetClientIpAddress();
            var device = GetClientDevice();
            var identifier = request.Username?.Trim() ?? string.Empty;

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Username == identifier ||
                u.BhxhCode == identifier ||
                u.Email == identifier);

            if (user == null)
            {
                await _logService.WriteLogAsync(identifier, "LOGIN_FAILED", $"Tài khoản không tồn tại. Device={device}", ipAddress);
                return Unauthorized(new { message = "Tài khoản hoặc mật khẩu không đúng." });
            }

            if (user.IsLocked)
            {
                await _logService.WriteLogAsync(user.Username, "LOGIN_BLOCKED", $"Tài khoản bị khóa bởi Admin. Device={device}", ipAddress);
                return StatusCode(403, new { message = "Tài khoản đã bị khóa bởi quản trị viên." });
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            {
                var waitTime = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                await _logService.WriteLogAsync(user.Username, "LOGIN_BLOCKED", $"Tài khoản đang bị tạm khóa. Device={device}", ipAddress);
                return StatusCode(403, new { message = $"Tài khoản đang tạm khóa. Vui lòng thử lại sau {waitTime} phút." });
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.FailedLoginAttempts += 1;

                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    await _logService.WriteLogAsync(
                        user.Username,
                        "ACCOUNT_LOCKED",
                        $"Khóa tạm {LockoutMinutes} phút do sai mật khẩu {MaxFailedAttempts} lần. Device={device}",
                        ipAddress);
                }
                else
                {
                    await _logService.WriteLogAsync(
                        user.Username,
                        "LOGIN_FAILED",
                        $"Sai mật khẩu lần {user.FailedLoginAttempts}/{MaxFailedAttempts}. Device={device}",
                        ipAddress);
                }

                await _context.SaveChangesAsync();
                return Unauthorized(new { message = "Tài khoản hoặc mật khẩu không đúng." });
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return BadRequest(new { message = "Tài khoản chưa có email, không thể bật OTP đăng nhập." });
            }

            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _context.SaveChangesAsync();

            try
            {
                await _otpService.CreateAndSendOtpAsync(user.Email, OtpPurpose.Login, user.Id, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                await _logService.WriteLogAsync(user.Username, "LOGIN_OTP_FAILED", $"Gửi OTP đăng nhập thất bại: {ex.Message}. Device={device}", ipAddress);
                return StatusCode(500, new { message = "Không thể gửi OTP đăng nhập. Vui lòng thử lại." });
            }

            await _logService.WriteLogAsync(user.Username, "LOGIN_OTP_SENT", $"Đã gửi OTP đăng nhập. Device={device}", ipAddress);

            return Ok(new
            {
                requiresOtp = true,
                purpose = OtpPurpose.Login,
                email = user.Email,
                maskedEmail = MaskEmail(user.Email),
                message = "Đã gửi OTP đến email đăng ký. Vui lòng xác thực để hoàn tất đăng nhập."
            });
        }

        [HttpPost("verify-login-otp")]
        [HttpPost("/api/verify-login-otp")]
        public async Task<IActionResult> VerifyLoginOtp([FromBody] VerifyLoginOtpDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var ipAddress = GetClientIpAddress();
            var email = request.Email.Trim().ToLowerInvariant();

            var otpRecord = await _otpService.VerifyOtpAsync(email, request.Otp, OtpPurpose.Login, true, HttpContext.RequestAborted);
            if (otpRecord == null)
            {
                return Unauthorized(new { message = "OTP đăng nhập không hợp lệ hoặc đã hết hạn." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == otpRecord.UserId);
            if (user == null || user.IsLocked)
            {
                return Unauthorized(new { message = "Không thể xác thực phiên đăng nhập." });
            }

            var normalizedRole = RoleHelper.Normalize(user.Role);
            if (!string.Equals(user.Role, normalizedRole, StringComparison.Ordinal))
            {
                user.Role = normalizedRole;
                await _context.SaveChangesAsync();
            }

            var token = CreateToken(user, normalizedRole);
            var redirectPath = RoleHelper.GetDashboardPath(normalizedRole);

            await _logService.WriteLogAsync(user.Username, "LOGIN_SUCCESS", "Đăng nhập thành công sau khi xác thực OTP.", ipAddress);
            return Ok(new
            {
                token,
                role = normalizedRole,
                redirectPath,
                message = "Đăng nhập thành công."
            });
        }

        [HttpPost("forgot-password")]
        [HttpPost("/api/forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var ipAddress = GetClientIpAddress();
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                try
                {
                    await _otpService.CreateAndSendOtpAsync(email, OtpPurpose.Reset, user.Id, HttpContext.RequestAborted);
                    await _logService.WriteLogAsync(user.Username, "RESET_OTP_SENT", "Đã gửi OTP đặt lại mật khẩu.", ipAddress);
                }
                catch (Exception ex)
                {
                    await _logService.WriteLogAsync(user.Username, "RESET_OTP_FAILED", $"Gửi OTP quên mật khẩu thất bại: {ex.Message}", ipAddress);
                    return StatusCode(500, new { message = "Không thể gửi OTP đặt lại mật khẩu. Vui lòng thử lại." });
                }
            }

            return Ok(new
            {
                message = "Nếu email tồn tại trong hệ thống, OTP đặt lại mật khẩu đã được gửi."
            });
        }

        [HttpPost("reset-password")]
        [HttpPost("/api/reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (!PasswordPolicyHelper.IsStrong(request.NewPassword))
            {
                return BadRequest(new { message = PasswordPolicyHelper.PolicyDescription });
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var otpRecord = await _otpService.VerifyOtpAsync(email, request.Otp, OtpPurpose.Reset, true, HttpContext.RequestAborted);
            if (otpRecord == null)
            {
                return BadRequest(new { message = "OTP không hợp lệ hoặc đã hết hạn." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return BadRequest(new { message = "Không tìm thấy tài khoản tương ứng." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _context.SaveChangesAsync();

            await _logService.WriteLogAsync(user.Username, "RESET_PASSWORD_SUCCESS", "Đặt lại mật khẩu thành công.");
            return Ok(new { message = "Đặt lại mật khẩu thành công." });
        }

        [HttpGet("login-history")]
        [Authorize]
        public async Task<IActionResult> GetLoginHistory()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized();
            }

            var entries = await _context.SystemLogs
                .Where(l => l.Username == username && (l.Action == "LOGIN_SUCCESS" || l.Action == "LOGIN_FAILED" || l.Action == "LOGIN_BLOCKED"))
                .OrderByDescending(l => l.CreatedAt)
                .Take(10)
                .Select(l => new
                {
                    action = l.Action,
                    ipAddress = l.IpAddress,
                    time = l.CreatedAt,
                    details = l.Content
                })
                .ToListAsync();

            return Ok(entries);
        }

        private string CreateToken(User user, string normalizedRole)
        {
            var jwtKey = ConfigurationHelper.GetJwtSecret(_configuration)
                ?? throw new InvalidOperationException("JWT secret not configured.");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, normalizedRole)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GetClientIpAddress()
        {
            var xForwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xForwardedFor))
            {
                return xForwardedFor.Split(',').First().Trim();
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
        }

        private string GetClientDevice()
        {
            var userAgent = Request.Headers["User-Agent"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return "Unknown Device";
            }

            return userAgent.Length > 160 ? userAgent[..160] : userAgent;
        }

        private static string MaskEmail(string email)
        {
            var trimmed = (email ?? string.Empty).Trim();
            var atIndex = trimmed.IndexOf('@');
            if (atIndex <= 1)
            {
                return "***";
            }

            var userPart = trimmed[..atIndex];
            var domainPart = trimmed[atIndex..];
            return $"{userPart[0]}***{userPart[^1]}{domainPart}";
        }
    }
}
