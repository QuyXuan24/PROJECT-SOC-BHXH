using System.ComponentModel.DataAnnotations;

namespace BHXH_Backend.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty; // Lưu chuỗi đã mã hóa, không lưu pass thường

        public string FullName { get; set; } = string.Empty;

        public string Role { get; set; } = "User"; // Phân quyền: User, Admin, Officer
        
        public string BhxhCode { get; set; } = string.Empty; // Mã số BHXH (sẽ mã hóa AES)

        public bool IsLocked { get; set; } = false; // Trạng thái khóa tài khoản
    }
}