using System.ComponentModel.DataAnnotations;

namespace BHXH_Backend.Dtos
{
    // Dữ liệu client gửi lên khi Đăng ký
    public class RegisterDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MinLength(6, ErrorMessage = "Mật khẩu phải dài ít nhất 6 ký tự")]
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public string FullName { get; set; } = string.Empty;
    }

    // Dữ liệu client gửi lên khi Đăng nhập
    public class LoginDto
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}