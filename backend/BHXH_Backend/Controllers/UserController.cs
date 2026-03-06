using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BHXH_Backend.Data;
using BHXH_Backend.Models;
using BHXH_Backend.Helpers; 
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BHXH_Backend.Controllers
{
    // DTO: Khuôn mẫu dữ liệu User gửi lên (Đủ 7 trường bạn yêu cầu)
    public class UserProfileDto
    {
        // Nhóm 1: Thông tin cơ bản (Không mã hóa)
        public required string FullName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public required string Gender { get; set; } // "Nam", "Nữ"

        // Nhóm 2: Thông tin nhạy cảm (Sẽ Mã hóa AES)
        public required string Cccd { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Address { get; set; }
        public required string BhxhCode { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // <--- Bắt buộc phải Login mới được vào
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public UserController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // 1. NGƯỜI DÂN NỘP HỒ SƠ (POST)
        [HttpPost("profile")]
        public async Task<IActionResult> SubmitProfile([FromBody] UserProfileDto req)
        {
            // Lấy ID người đang đăng nhập
            var userIdStr = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            var userId = int.Parse(userIdStr);

            // Kiểm tra xem đã nộp chưa
            var record = await _context.BhxhRecords.FirstOrDefaultAsync(r => r.UserId == userId);
            
            // Lấy chìa khóa bí mật
            string aesKey = _config["AesSettings:Key"];

            if (record == null)
            {
                // --- TẠO MỚI ---
                record = new BhxhRecord
                {
                    UserId = userId,
                    
                    // 1. Lưu thẳng (để tìm kiếm)
                    FullName = req.FullName,
                    DateOfBirth = req.DateOfBirth,
                    Gender = req.Gender,

                    // 2. Mã hóa (để bảo mật)
                    Cccd = SecurityHelper.Encrypt(req.Cccd, aesKey),
                    PhoneNumber = SecurityHelper.Encrypt(req.PhoneNumber, aesKey),
                    Address = SecurityHelper.Encrypt(req.Address, aesKey),
                    BhxhCode = SecurityHelper.Encrypt(req.BhxhCode, aesKey),

                    Status = "Pending", // Mặc định là Chờ duyệt
                    CreatedAt = DateTime.UtcNow
                };
                _context.BhxhRecords.Add(record);
            }
            else
            {
                // --- CẬP NHẬT ---
                if (record.Status == "Approved") 
                    return BadRequest("Hồ sơ đã được duyệt, bạn không thể tự sửa nữa!");

                record.FullName = req.FullName;
                record.DateOfBirth = req.DateOfBirth;
                record.Gender = req.Gender;

                // Mã hóa lại thông tin mới
                record.Cccd = SecurityHelper.Encrypt(req.Cccd, aesKey);
                record.PhoneNumber = SecurityHelper.Encrypt(req.PhoneNumber, aesKey);
                record.Address = SecurityHelper.Encrypt(req.Address, aesKey);
                record.BhxhCode = SecurityHelper.Encrypt(req.BhxhCode, aesKey);

                record.Status = "Pending"; // Reset lại trạng thái chờ duyệt
                record.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Nộp hồ sơ thành công! Vui lòng chờ Staff xét duyệt." });
        }

        // 2. NGƯỜI DÂN XEM HỒ SƠ CỦA MÌNH (GET)
        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = int.Parse(User.FindFirst("id")?.Value);
            var record = await _context.BhxhRecords.FirstOrDefaultAsync(r => r.UserId == userId);

            if (record == null) return NotFound("Bạn chưa nộp hồ sơ nào.");

            string aesKey = _config["AesSettings:Key"];

            // Trả về dữ liệu đã GIẢI MÃ (Decrypt) cho chính chủ xem
            return Ok(new 
            {
                Status = record.Status,
                Note = record.Status == "Pending" ? "Đang chờ duyệt" : "Đã được duyệt",
                
                // Thông tin công khai
                FullName = record.FullName,
                DateOfBirth = record.DateOfBirth.ToString("dd/MM/yyyy"), // Format ngày cho đẹp
                Gender = record.Gender,

                // Thông tin giải mã
                Cccd = SecurityHelper.Decrypt(record.Cccd, aesKey),
                PhoneNumber = SecurityHelper.Decrypt(record.PhoneNumber, aesKey),
                Address = SecurityHelper.Decrypt(record.Address, aesKey),
                BhxhCode = SecurityHelper.Decrypt(record.BhxhCode, aesKey)
            });
        }
    }
}