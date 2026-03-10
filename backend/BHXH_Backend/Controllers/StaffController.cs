using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BHXH_Backend.Data;
using BHXH_Backend.Models;
using BHXH_Backend.Dtos; // Import DTO mới tạo
using BHXH_Backend.Services; 
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Staff")] // Bảo vệ controller này chỉ cho nhân viên
    public class StaffController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logger;

        public StaffController(ApplicationDbContext context, SystemLogService logger)
        {
            _context = context;
            _logger = logger;
        }

        // 1. Xem danh sách hồ sơ cần duyệt
        [HttpGet("pending-records")]
        public async Task<IActionResult> GetPendingRecords()
        {
            try 
            {
                var records = await _context.BhxhRecords
                                            .Where(r => r.Status == "Pending")
                                            .ToListAsync();
                return Ok(records);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi kết nối database: " + ex.Message });
            }
        }

        // 2. Xử lý hồ sơ 
        [HttpPut("process-record/{id}")]
            public async Task<IActionResult> ProcessRecord(int id, [FromBody] ProcessRecordDto req) 
            {
                var record = await _context.BhxhRecords.FindAsync(id);
                if (record == null) return NotFound(new { message = "Hồ sơ không tồn tại" });

                // --- CHỐT CHẶN NGHIỆP VỤ ---
                // Hồ sơ đã duyệt rồi thì KHÔNG ĐƯỢC PHÉP sửa nữa
                if (record.Status == "Approved") 
                {
                    return BadRequest(new { message = "Hồ sơ đã được duyệt, không thể thay đổi trạng thái!" });
                }

                // Cập nhật trạng thái
                record.Status = req.Action; 
                record.ProcessedBy = User.FindFirst(ClaimTypes.Name)?.Value;
                record.UpdatedAt = DateTime.UtcNow; 
                
                try 
                {
                    await _context.SaveChangesAsync();

                    await _logger.WriteLogAsync(
                        User.Identity?.Name, 
                        "PROCESS_RECORD", 
                        $"Nhân viên đã {req.Action} hồ sơ ID: {id}"
                    );
                    
                    string messageAction = (req.Action == "Approved") ? "duyệt" : "từ chối";
                    return Ok(new { message = $"Đã {messageAction} hồ sơ thành công!" });
                }
                catch (DbUpdateException ex)
                {
                    await _logger.WriteLogAsync("System", "DB_ERROR", $"Lỗi cập nhật hồ sơ {id}: {ex.Message}");
                    return StatusCode(500, new { message = "Lỗi lưu dữ liệu vào hệ thống." });
                }
            }
    }
}