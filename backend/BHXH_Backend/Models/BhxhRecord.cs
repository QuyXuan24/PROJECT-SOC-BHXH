using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BHXH_Backend.Models
{
    public class BhxhRecord
    {
        [Key]
        public int Id { get; set; }
        public string? ProcessedBy { get; set; }

        // Liên kết với User
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        // --- NHÓM 1: DỮ LIỆU CƠ BẢN (Không mã hóa để Staff tìm kiếm) ---
        public required string FullName { get; set; }
        public DateTime DateOfBirth { get; set; } // Ngày sinh
        public required string Gender { get; set; } // Nam/Nữ

        // --- NHÓM 2: DỮ LIỆU NHẠY CẢM (MÃ HÓA AES-256) ---
        // Hacker mở DB ra chỉ thấy chuỗi loằng ngoằng
        public required string Cccd { get; set; }         // Số CCCD
        public required string PhoneNumber { get; set; }  // Số điện thoại
        public required string Address { get; set; }      // Địa chỉ cư trú
        public required string BhxhCode { get; set; }     // Mã số BHXH

        // --- QUẢN LÝ TRẠNG THÁI ---
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}