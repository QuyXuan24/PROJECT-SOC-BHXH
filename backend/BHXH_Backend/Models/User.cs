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

        public string PhoneNumber { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = "User"; // Phân quyền: User, Employee, Security, Admin
        
        public string BhxhCode { get; set; } = string.Empty; // Mã số BHXH (sẽ mã hóa AES)

        public bool IsLocked { get; set; } = false; // Trạng thái khóa tài khoản

        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; }
    }
}
