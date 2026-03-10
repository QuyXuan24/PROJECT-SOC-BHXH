using BHXH_Backend.Data;
using BHXH_Backend.Models;

namespace BHXH_Backend.Services
{
    public class SystemLogService
    {
        private readonly ApplicationDbContext _context;

        public SystemLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Hàm này sẽ được gọi ở bất cứ đâu cần ghi log
        public async Task WriteLogAsync(string? username, string action, string content,string? ipAddress = null)
        {
            var log = new SystemLog
            {
                Username = username ?? "Unknown",
                Action = action,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                IpAddress = ipAddress ?? "Unknown IP"
            };

            _context.SystemLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}