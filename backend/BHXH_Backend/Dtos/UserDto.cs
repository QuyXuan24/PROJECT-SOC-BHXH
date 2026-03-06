using System.ComponentModel.DataAnnotations;

namespace BHXH_Backend.Dtos
{
    // Dữ liệu client gửi lên khi Đăng ký
    public class RegisterDto
    {
        [Required(ErrorMessage = "Username là bắt buộc")]
        [StringLength(20, MinimumLength = 4, ErrorMessage = "Username phải từ 4-20 ký tự")]
        [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Username chỉ được chứa chữ và số")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải từ 8-100 ký tự")]
        // Regex bắt buộc: 1 chữ hoa, 1 chữ thường, 1 số, 1 ký tự đặc biệt
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", 
            ErrorMessage = "Mật khẩu cần: 1 chữ hoa, 1 chữ thường, 1 số và 1 ký tự đặc biệt")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên là bắt buộc")]
        [StringLength(100, ErrorMessage = "Tên quá dài")]
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