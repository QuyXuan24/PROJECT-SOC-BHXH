using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BHXH_Backend.Data;
using BHXH_Backend.Models;
using BHXH_Backend.Services; 
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BHXH_Backend.Controllers
{
    // DTO để nhận dữ liệu
    public class CreateEmployeeDto
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string FullName { get; set; }
        public required string Role { get; set; }
    }

    public class UpdateUserDto
    {
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? Password { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logger; // <--- Fix lỗi thiếu biến _logger

        // Constructor: Fix lỗi thiếu Inject
        public AdminController(ApplicationDbContext context, SystemLogService logger)
        {
            _context = context;
            _logger = logger;
        }

        // 1. Lấy danh sách user
        [HttpGet("users")]
        public IActionResult GetAllUsers()
        {
            var users = _context.Users
                .Select(u => new { u.Id, u.Username, u.FullName, u.Role, u.IsLocked })
                .ToList();
            return Ok(users);
        }

        // 2. Tạo nhân viên (Fix lỗi thiếu async)
        [HttpPost("users")]
        public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeDto req)
        {
            if (_context.Users.Any(u => u.Username == req.Username))
                return BadRequest("Tên đăng nhập này đã tồn tại!");

            if (req.Role != "Staff" && req.Role != "SOC")
                return BadRequest("Chỉ được tạo tài khoản quyền 'Staff' hoặc 'SOC'.");

            var newUser = new User
            {
                Username = req.Username,
                FullName = req.FullName,
                Role = req.Role,
                IsLocked = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // GHI LOG
            var adminName = User.FindFirst("username")?.Value;
            await _logger.WriteLogAsync(adminName, "CREATE_USER", $"Đã tạo nhân viên mới: {req.Username} ({req.Role})");

            return Ok(new { message = $"Đã tạo nhân viên {req.Username} quyền {req.Role}" });
        }

        // 3. Sửa nhân viên (Fix lỗi thiếu async)
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto req)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound("Không tìm thấy nhân viên này");

            if (!string.IsNullOrEmpty(req.FullName)) user.FullName = req.FullName;
            if (!string.IsNullOrEmpty(req.Role))
            {
                if (req.Role != "Staff" && req.Role != "SOC") return BadRequest("Role không hợp lệ");
                user.Role = req.Role;
            }
            if (!string.IsNullOrEmpty(req.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            }

            await _context.SaveChangesAsync();

            // GHI LOG
            var adminName = User.FindFirst("username")?.Value;
            await _logger.WriteLogAsync(adminName, "UPDATE_USER", $"Đã cập nhật User ID {id} ({user.Username})");

            return Ok(new { message = "Đã cập nhật thông tin thành công" });
        }

        // 4. Khóa/Mở khóa (Fix lỗi thiếu async)
        [HttpPut("users/{id}/lock")]
        public async Task<IActionResult> ToggleLock(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound("Không tìm thấy nhân viên này");

            user.IsLocked = !user.IsLocked;
            await _context.SaveChangesAsync();

            string statusText = user.IsLocked ? "KHÓA" : "MỞ KHÓA";
            var adminName = User.FindFirst("username")?.Value;
            await _logger.WriteLogAsync(adminName, "LOCK_USER", $"Đã {statusText} tài khoản {user.Username} (ID: {id})");

            return Ok(new { message = $"Đã {statusText} tài khoản {user.Username} thành công" });
        }
    }
}