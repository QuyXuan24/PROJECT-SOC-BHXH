using BHXH_Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
// ĐÃ XÓA: using Microsoft.OpenApi.Models; (Nguyên nhân gây lỗi)

var builder = WebApplication.CreateBuilder(args);

// 1. KẾT NỐI DATABASE
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. CẤU HÌNH BẢO MẬT JWT (Vẫn giữ logic bảo mật, chỉ bỏ giao diện ổ khóa)
var jwtKey = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "khoa_bi_mat_mac_dinh_dai_hon_32_ky_tu_abcd";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = false, 
            ValidateAudience = false
        };
    });

builder.Services.AddControllers();

// 3. SWAGGER ĐƠN GIẢN (Theo chuẩn mới)
// Không cấu hình rườm rà nữa, để mặc định cho nó tự chạy
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); 
builder.Services.AddScoped<BHXH_Backend.Services.SystemLogService>();

var app = builder.Build();

// AUTO MIGRATION (Tự tạo bảng nếu chưa có)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// CẤU HÌNH HTTP REQUEST
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication(); // Xác thực
app.UseAuthorization();  // Phân quyền

app.MapControllers();

app.Run();