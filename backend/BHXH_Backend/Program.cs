using BHXH_Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// 1. KẾT NỐI DATABASE
var connectionString =  Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
?? builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();


// AUTO MIGRATION (Tự tạo bảng nếu chưa có)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<ApplicationDbContext>();

    try
    {
        logger.LogInformation("Dang cho SQL Server khoi dong...");
        
        // Thử kết nối 10 lần, mỗi lần cách nhau 5 giây
        int retries = 10;
        while (retries > 0)
        {
            try
            {
                if (context.Database.CanConnect())
                {
                    logger.LogInformation("Da ket noi Database thanh cong! Dang Migrate...");
                    context.Database.Migrate(); // Tạo bảng
                    break; // Thành công thì thoát vòng lặp
                }
            }
            catch (Exception)
            {
                retries--;
                if (retries == 0) throw; // Hết lượt thì báo lỗi
                logger.LogWarning($"Ket noi that bai. Thu lai sau 5s... (Con {retries} lan)");
                System.Threading.Thread.Sleep(5000); // Chờ 5 giây
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "LOI NGHIEM TRONG: Khong the ket noi Database!");
    }
}

// CẤU HÌNH HTTP REQUEST
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseMiddleware<BHXH_Backend.Middlewares.RequestLoggingMiddleware>();

//app.UseHttpsRedirection();

app.UseCors("AllowAll");
app.UseAuthentication(); // Xác thực
app.UseAuthorization();  // Phân quyền

app.MapControllers();

app.Run();