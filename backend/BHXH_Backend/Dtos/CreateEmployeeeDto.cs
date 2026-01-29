namespace BHXH_Backend.Dtos
{
    public class CreateEmployeeDto
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string FullName { get; set; }
        
        // Admin phải chọn chức vụ cho nhân viên (Staff hoặc SOC)
        public required string Role { get; set; } 
    }
}