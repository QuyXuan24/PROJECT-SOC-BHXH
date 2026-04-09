using System.Security.Cryptography;
using BHXH_Backend.Data;
using BHXH_Backend.Helpers;
using BHXH_Backend.Models;
using BHXH_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Controllers
{
    public class CreateAccountDto
    {
        public required string Username { get; set; }
        public required string FullName { get; set; }
        public required string Role { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Password { get; set; }
    }

    public class UpdateUserDto
    {
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? Password { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class ToggleLockRequest
    {
        public string? Reason { get; set; }
        public int? DurationMinutes { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logger;

        public AdminController(ApplicationDbContext context, SystemLogService logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("users")]
        public IActionResult GetAllUsers()
        {
            var users = _context.Users
                .OrderBy(u => u.Id)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.FullName,
                    u.Email,
                    u.PhoneNumber,
                    Role = RoleHelper.Normalize(u.Role),
                    u.IsLocked,
                    u.LockoutEnd
                })
                .ToList();

            return Ok(users);
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountDto req)
        {
            var role = RoleHelper.Normalize(req.Role);
            if (!RoleHelper.IsSupportedRole(role))
            {
                return BadRequest("Role khong hop le.");
            }

            if (await _context.Users.AnyAsync(u => u.Username == req.Username))
            {
                return BadRequest("Ten dang nhap da ton tai.");
            }

            if (!string.IsNullOrWhiteSpace(req.Email) && await _context.Users.AnyAsync(u => u.Email == req.Email))
            {
                return BadRequest("Email da duoc su dung.");
            }

            if (!string.IsNullOrWhiteSpace(req.PhoneNumber) && await _context.Users.AnyAsync(u => u.PhoneNumber == req.PhoneNumber))
            {
                return BadRequest("So dien thoai da duoc su dung.");
            }

            var rawPassword = string.IsNullOrWhiteSpace(req.Password) ? GenerateTemporaryPassword() : req.Password.Trim();
            if (!PasswordPolicyHelper.IsStrong(rawPassword))
            {
                return BadRequest(PasswordPolicyHelper.PolicyDescription);
            }

            var newUser = new User
            {
                Username = req.Username.Trim(),
                FullName = req.FullName.Trim(),
                Role = role,
                IsLocked = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword),
                PhoneNumber = req.PhoneNumber?.Trim() ?? string.Empty,
                Email = req.Email?.Trim() ?? string.Empty,
                BhxhCode = string.Empty,
                FailedLoginAttempts = 0
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            var adminName = User.Identity?.Name;
            await _logger.WriteLogAsync(adminName, "CREATE_USER", $"Admin tao tai khoan moi: {newUser.Username} ({newUser.Role})");

            return Ok(new
            {
                message = $"Da tao tai khoan {newUser.Username} thanh cong.",
                role,
                temporaryPassword = req.Password is null ? rawPassword : null,
                note = req.Password is null ? "Can gui mat khau tam qua email/SMS o buoc tiep theo." : null
            });
        }

        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto req)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound("Khong tim thay tai khoan.");
            }

            if (!string.IsNullOrWhiteSpace(req.FullName))
            {
                user.FullName = req.FullName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(req.Email))
            {
                var email = req.Email.Trim();
                if (await _context.Users.AnyAsync(u => u.Email == email && u.Id != id))
                {
                    return BadRequest("Email da duoc su dung.");
                }

                user.Email = email;
            }

            if (!string.IsNullOrWhiteSpace(req.PhoneNumber))
            {
                var phone = req.PhoneNumber.Trim();
                if (await _context.Users.AnyAsync(u => u.PhoneNumber == phone && u.Id != id))
                {
                    return BadRequest("So dien thoai da duoc su dung.");
                }

                user.PhoneNumber = phone;
            }

            if (!string.IsNullOrWhiteSpace(req.Role))
            {
                var role = RoleHelper.Normalize(req.Role);
                if (!RoleHelper.IsSupportedRole(role))
                {
                    return BadRequest("Role khong hop le.");
                }

                user.Role = role;
            }

            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                if (!PasswordPolicyHelper.IsStrong(req.Password))
                {
                    return BadRequest(PasswordPolicyHelper.PolicyDescription);
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            }

            await _context.SaveChangesAsync();

            var adminName = User.Identity?.Name;
            await _logger.WriteLogAsync(adminName, "UPDATE_USER", $"Admin cap nhat user ID={id} ({user.Username})");

            return Ok(new { message = "Cap nhat tai khoan thanh cong." });
        }

        [HttpPut("users/{id}/lock")]
        public async Task<IActionResult> ToggleLock(int id, [FromBody] ToggleLockRequest? request)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound("Khong tim thay tai khoan.");
            }

            var adminName = User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(adminName) && string.Equals(adminName, user.Username, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Admin khong the khoa chinh minh.");
            }

            if (user.IsLocked || (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow))
            {
                user.IsLocked = false;
                user.LockoutEnd = null;
                user.FailedLoginAttempts = 0;

                await _context.SaveChangesAsync();
                await _logger.WriteLogAsync(adminName, "UNLOCK_USER", $"Admin mo khoa tai khoan {user.Username}");
                return Ok(new { message = $"Da mo khoa tai khoan {user.Username}." });
            }

            var reason = request?.Reason?.Trim() ?? string.Empty;
            if (reason.Length < 10)
            {
                return BadRequest("Vui long nhap ly do khoa toi thieu 10 ky tu.");
            }

            user.IsLocked = true;
            if (request?.DurationMinutes is int durationMinutes && durationMinutes > 0)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(durationMinutes);
            }
            else
            {
                user.LockoutEnd = null;
            }

            await _context.SaveChangesAsync();
            await _logger.WriteLogAsync(adminName, "LOCK_USER", $"Admin khoa tai khoan {user.Username}. Ly do: {reason}");

            return Ok(new { message = $"Da khoa tai khoan {user.Username}." });
        }

        private static string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789@$!%*?&";
            Span<byte> random = stackalloc byte[12];
            RandomNumberGenerator.Fill(random);

            var passwordChars = new char[12];
            for (var i = 0; i < random.Length; i++)
            {
                passwordChars[i] = chars[random[i] % chars.Length];
            }

            var basePassword = new string(passwordChars);
            // Dam bao du policy boi vi password random co the thieu 1 nhom.
            return $"Aa1@{basePassword}";
        }
    }
}
