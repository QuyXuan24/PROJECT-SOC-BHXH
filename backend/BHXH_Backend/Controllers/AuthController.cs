using Microsoft.AspNetCore.Mvc;
using BHXH_Backend.Data;
using BHXH_Backend.Models;
using BHXH_Backend.Dtos;
using Microsoft.EntityFrameworkCore;
using BHXH_Backend.Services;
using BCrypt.Net;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
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

            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                await _logService.WriteLogAsync(request.Username, "REGISTER_FAILED", "Tài khoản đã tồn tại.", ipAddress);
                return BadRequest(new { message = "Tài khoản đã tồn tại." });
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = new User
            {
                Username = request.Username,
                PasswordHash = passwordHash,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                Role = "User",
                FailedLoginAttempts = 0,
                BhxhCode = request.BhxhCode?.Trim() ?? string.Empty
            };


            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _logService.WriteLogAsync(request.Username, "REGISTER_SUCCESS", "Đăng ký tài khoản mới thành công.", ipAddress);

            return Ok(new { message = "Đăng ký thành công!" });
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(LoginDto request)
        {
            var ipAddress = GetClientIpAddress();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null)
            {
                await _logService.WriteLogAsync(request.Username ?? "Unknown", "LOGIN_FAILED", "Tài khoản không tồn tại.", ipAddress);
                return Unauthorized(new { message = "Tài khoản hoặc mật khẩu không đúng." });
            }

            // --- TẦNG 1: KIỂM TRA ADMIN LOCK (Khóa vĩnh viễn/thủ công) ---
            if (user.IsLocked)
            {
                await _logService.WriteLogAsync(user.Username, "LOGIN_BLOCKED", "Cố đăng nhập khi tài khoản bị Admin khóa vĩnh viễn.", ipAddress);
                return StatusCode(403, new { message = "Tài khoản của bạn đã bị quản trị viên khóa." });
            }

            // --- TÍNH NĂNG SOC: KIỂM TRA XEM TÀI KHOẢN CÓ ĐANG BỊ KHÓA KHÔNG ---
            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            {
                var waitTime = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                await _logService.WriteLogAsync(user.Username, "LOGIN_BLOCKED", $"Cố đăng nhập khi tài khoản đang bị khóa.", ipAddress);
                
                return StatusCode(403, new { message = $"Tài khoản đã bị khóa an toàn do có dấu hiệu dò mật khẩu. Vui lòng thử lại sau {waitTime} phút nữa." });
            }

            // --- TÍNH NĂNG SOC: KIỂM TRA MẬT KHẨU VÀ ĐẾM SỐ LẦN SAI ---
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.FailedLoginAttempts += 1; // Tăng số lần sai lên 1

                // Nếu sai 5 lần thì khóa 15 phút
                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    await _logService.WriteLogAsync(user.Username, "ACCOUNT_LOCKED", "Khóa tài khoản 15 phút do dò mật khẩu (Brute-force) 5 lần.", ipAddress);
                }
                else
                {
                    await _logService.WriteLogAsync(user.Username, "LOGIN_FAILED", $"Sai mật khẩu lần {user.FailedLoginAttempts}/5.", ipAddress);
                }

                await _context.SaveChangesAsync(); // Lưu trạng thái đếm vào DB
                return Unauthorized(new { message = "Tài khoản hoặc mật khẩu không đúng." });
            }

            // --- ĐĂNG NHẬP THÀNH CÔNG: Xóa lịch sử lỗi (Reset bộ đếm về 0) ---
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _context.SaveChangesAsync();

            await _logService.WriteLogAsync(user.Username, "LOGIN_SUCCESS", "Đăng nhập thành công.", ipAddress);

            string token = CreateToken(user);
            return Ok(new { token = token, message = "Đăng nhập thành công!" });
        }

        private string CreateToken(User user)
        {
            var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "khoa_bi_mat_mac_dinh_dai_hon_32_ky_tu_abcd";

            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
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
            var ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
            }
            return ipAddress;
        }
    }
}
