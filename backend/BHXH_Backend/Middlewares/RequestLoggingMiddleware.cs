using BHXH_Backend.Services;

namespace BHXH_Backend.Middlewares
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestLoggingMiddleware(RequestDelegate next)
        {
            _next = next; // _next chính là cánh cửa cho phép Request đi tiếp vào Controller
        }

        // Hàm này sẽ tự động chạy MỖI KHI có người gọi bất kỳ API nào
        // Lưu ý: Inject SystemLogService vào hàm InvokeAsync, không để ở Constructor
        public async Task InvokeAsync(HttpContext context, SystemLogService logService)
    {
    try
        {
        await _next(context);

        // --- ĐOẠN CODE BẮT IP THẬT (Xuyên qua Nginx) ---
        // Nginx thường giấu IP thật của user trong header "X-Forwarded-For"
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        
        // Nếu không có Nginx (chạy test Local), thì lấy trực tiếp từ Connection
        if (string.IsNullOrEmpty(ipAddress))
            {
            ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
            }
        // ------------------------------------------------

        if (context.Response.StatusCode == 401)
            {
            // Truyền thêm ipAddress vào cuối
            await logService.WriteLogAsync("Unknown", "UNAUTHORIZED_401", $"Truy cập trái phép vào {context.Request.Path}", ipAddress);
            }
        else if (context.Response.StatusCode == 403)
            {
            var username = context.User.Identity?.Name ?? "Unknown User";
            // Truyền thêm ipAddress vào cuối
            await logService.WriteLogAsync(username, "FORBIDDEN_403", $"Vượt quyền truy cập tại {context.Request.Path}", ipAddress);
            }
        }
    catch (Exception ex)
        {
        // Lấy IP khi có lỗi sập hệ thống
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
        var username = context.User.Identity?.Name ?? "Unknown";
        
        // Truyền thêm ipAddress vào cuối
        await logService.WriteLogAsync(username, "SYSTEM_ERROR_500", $"Lỗi nghiêm trọng tại {context.Request.Path}: {ex.Message}", ipAddress);
        
        throw; 
        }
    }
    }
}