using System;
using System.ComponentModel.DataAnnotations;

namespace BHXH_Backend.Models
{
    public class SystemLog
    {
        [Key]
        public int Id { get; set; }

        // Ai làm? (Lưu Username để dễ đọc, cho phép null nếu là khách vãng lai)
        public string? Username { get; set; }
        
        // Làm hành động gì? (LOGIN, CREATE_USER, LOCK_USER...)
        public required string Action { get; set; }

        // Chi tiết hành động (Ví dụ: "Đã khóa tài khoản nhanvien_A")
        public required string Content { get; set; }

        // Làm vào lúc nào? (Mặc định lấy giờ hiện tại)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Làm từ đâu? (Lưu địa chỉ IP để truy vết hacker)
        public string? IpAddress { get; set; }
    }
}