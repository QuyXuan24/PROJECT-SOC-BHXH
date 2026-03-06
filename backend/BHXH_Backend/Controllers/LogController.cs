using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BHXH_Backend.Data;
using BHXH_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LogController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // 1. API GET: XEM NHẬT KÝ (CHỈ ADMIN VÀ SOC ĐƯỢC XEM)
        // =========================================================
        [HttpGet]
        [Authorize(Roles = "Admin, SOC")] // 👇 Chốt chặn an ninh đặt riêng ở đây
        public async Task<IActionResult> GetSystemLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                // Lấy 100 dòng log mới nhất, cái mới hiện lên đầu
                var logs = await _context.SystemLogs
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống khi lấy log", error = ex.Message });
            }
        }

        // =========================================================
        // 2. API POST: GHI NHẬT KÝ (MỞ CỬA CHO HỆ THỐNG TỰ GHI)
        // =========================================================
        // Không đặt [Authorize] ở đây để ai (hoặc hệ thống) cũng có thể gửi log cảnh báo
        [HttpPost]
        [Authorize] // 👈 Bảo vệ API này, chỉ cho phép người dùng đã đăng nhập (Admin, SOC, Staff) mới được ghi log
        public async Task<IActionResult> WriteLog([FromBody] LogRequestDto request)
        {
            // TỰ ĐỘNG BẮT IP: Chụp ngay IP của kẻ vừa gửi request
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

            var newLog = new SystemLog
            {
                Username = request.Username ?? "Khách vãng lai / Hệ thống",
                Action = request.Action,
                Content = request.Content,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            };

            _context.SystemLogs.Add(newLog);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã ghi log thành công!", data = newLog });
        }
    }

    // Class phụ (DTO) để nhận dữ liệu từ Frontend gửi lên khi muốn ghi log
    public class LogRequestDto
    {
        public string? Username { get; set; }
        public required string Action { get; set; }
        public required string Content { get; set; }
    }
}