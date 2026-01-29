namespace BHXH_Backend.Dtos
{
    public class UpdateEmployeeDto
    {
        // Các trường này có dấu ? (nullable) nghĩa là:
        // Nếu Admin để trống -> Giữ nguyên cái cũ.
        // Nếu Admin điền -> Thì mới sửa.
        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? Password { get; set; } // Dùng khi nhân viên quên pass, Admin reset lại
    }
}