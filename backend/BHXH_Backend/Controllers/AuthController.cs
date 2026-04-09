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

        public AuthController(ApplicationDbContext context, SystemLogService logService)
        {
            _context = context;
            _logService = logService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<User>> Register(RegisterDto request)
        {
            var ipAddress = GetClientIpAddress();
            var device = GetClientDevice();

            var bhxhCode = request.BhxhCode?.Trim() ?? string.Empty;
            if (!Regex.IsMatch(bhxhCode, "^\\d{10}$"))
            {
                return BadRequest(new { message = "Mã số BHXH phải đúng định dạng 10 chữ số." });
            }

            if (!PasswordPolicyHelper.IsStrong(request.Password))
            {
                return BadRequest(new { message = PasswordPolicyHelper.PolicyDescription });
            }

            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                await _logService.WriteLogAsync(request.Username, "REGISTER_FAILED", "Tên đăng nhập đã tồn tại.", ipAddress);
                return BadRequest(new { message = "Tên đăng nhập đã tồn tại." });
            }

            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                await _logService.WriteLogAsync(request.Username, "REGISTER_FAILED", "Email đã tồn tại.", ipAddress);
                return BadRequest(new { message = "Email đã được sử dụng." });
            }

            if (await _context.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber))
            {
                await _logService.WriteLogAsync(request.Username, "REGISTER_FAILED", "Số điện thoại đã tồn tại.", ipAddress);
                return BadRequest(new { message = "Số điện thoại đã được sử dụng." });
            }

            if (await _context.Users.AnyAsync(u => u.BhxhCode == bhxhCode))
            {
                await _logService.WriteLogAsync(request.Username, "REGISTER_FAILED", "Mã số BHXH đã tồn tại.", ipAddress);
                return BadRequest(new { message = "Mã số BHXH đã được sử dụng." });
            }

            var user = new User
            {
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                Role = RoleHelper.User,
                FailedLoginAttempts = 0,
                BhxhCode = bhxhCode
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _logService.WriteLogAsync(
                request.Username,
                "REGISTER_SUCCESS",
                $"Đăng ký thành công. Device={device}",
                ipAddress);

            return Ok(new
            {
                message = "Đăng ký thành công. (Lượt demo chưa bật OTP email/SMS)",
                requiresOtp = false
            });
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(LoginDto request)
        {
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

            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            var normalizedRole = RoleHelper.Normalize(user.Role);
            if (!string.Equals(user.Role, normalizedRole, StringComparison.Ordinal))
            {
                user.Role = normalizedRole;
            }

            await _context.SaveChangesAsync();

            await _logService.WriteLogAsync(
                user.Username,
                "LOGIN_SUCCESS",
                $"Đăng nhập thành công. Device={device}",
                ipAddress);

            var token = CreateToken(user, normalizedRole);
            var redirectPath = RoleHelper.GetDashboardPath(normalizedRole);

            return Ok(new
            {
                token,
                role = normalizedRole,
                redirectPath,
                message = "Đăng nhập thành công!"
            });
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
            var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? HttpContext.RequestServices.GetRequiredService<IConfiguration>()["JwtSettings:SecretKey"]
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
    }
}
