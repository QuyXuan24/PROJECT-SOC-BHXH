using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BHXH_Backend.Data;
using BHXH_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // 👇 CHỐT CHẶN AN NINH Ở ĐÂY 👇
    // Chỉ Admin và SOC mới được gọi API này. Staff gọi vào là bị lỗi 403 Forbidden ngay.
    [Authorize(Roles = "Admin, SOC")] 
    public class LogController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LogController(ApplicationDbContext context)
        {
            _context = context;
        }

        // API: Xem toàn bộ nhật ký hệ thống
        // GET: api/Log
        [HttpGet]
        public async Task<IActionResult> GetSystemLogs()
        {
            // Lấy 100 dòng log mới nhất (Sắp xếp giảm dần theo thời gian)
            // Không nên lấy hết hàng triệu dòng kẻo sập web
            var logs = await _context.SystemLogs
                .OrderByDescending(l => l.CreatedAt) // Cái mới nhất hiện lên đầu
                .Take(100) // Chỉ lấy 100 cái xem cho nhanh
                .ToListAsync();

            return Ok(logs);
        }
    }
}