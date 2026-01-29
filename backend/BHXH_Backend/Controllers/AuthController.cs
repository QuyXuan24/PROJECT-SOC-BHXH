using Microsoft.AspNetCore.Mvc;
using BHXH_Backend.Data;
using BHXH_Backend.Models;
using BHXH_Backend.Dtos;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net; // Thư viện mã hóa

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/Auth/register
        [HttpPost("register")]
        public async Task<ActionResult<User>> Register(RegisterDto request)
        {
            // 1. Kiểm tra xem username đã tồn tại chưa (Tránh trùng lặp)
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest("Tài khoản đã tồn tại.");
            }

            // 2. THỰC HIỆN TASK 12: HASH MẬT KHẨU
            // Tạo ra chuỗi mật khẩu đã mã hóa (Salt + Hash)
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            //Tạo User mới
            var user = new User
            {
                Username = request.Username,
                PasswordHash = passwordHash,
                FullName = request.FullName,
                Role = request.Role 
            };

            // 4. Lưu vào Database
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok("Đăng ký thành công!");
        }

        // POST: api/Auth/login
        [HttpPost("login")]
        public async Task<ActionResult<string>> Login(LoginDto request)
        {
            // 1. Tìm user trong DB
           var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            
            // 2. Kiểm tra user và verify mật khẩu (So sánh hash)
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return BadRequest("Tài khoản hoặc mật khẩu không đúng.");
            }

            // 3. Tạo Token (Thẻ bài) - Đã chứa thông tin Role
            string token = CreateToken(user);
            return Ok(token);
        }

        // Hàm phụ trợ để tạo JWT Token
        private string CreateToken(User user)
        {
            var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "khoa_bi_mat_mac_dinh_dai_hon_32_ky_tu_abcd";

            // TẠO CLAIMS (THÔNG TIN IN TRÊN THẺ)
            List<System.Security.Claims.Claim> claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim("username", user.Username),
                new System.Security.Claims.Claim("role", user.Role) 
            };

            var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey));
            var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddDays(1), // Token có hạn 1 ngày
                signingCredentials: creds
            );

            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
            return jwt;
        }
    }
}