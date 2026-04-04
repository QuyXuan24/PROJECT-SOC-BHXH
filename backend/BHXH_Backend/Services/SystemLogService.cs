using BHXH_Backend.Data;
using BHXH_Backend.Models;

namespace BHXH_Backend.Services
{
    public class SystemLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly BlockchainService _blockchainService;
        private readonly ILogger<SystemLogService> _logger;

        public SystemLogService(
            ApplicationDbContext context,
            BlockchainService blockchainService,
            ILogger<SystemLogService> logger)
        {
            _context = context;
            _blockchainService = blockchainService;
            _logger = logger;
        }

        // Ham nay se duoc goi o bat ky dau can ghi log.
        // Log van luu SQL ngay ca khi bridge blockchain tam thoi loi.
        public async Task WriteLogAsync(
            string? username,
            string action,
            string content,
            string? ipAddress = null)
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

            // Best-effort: khong lam fail business flow neu blockchain bi gian doan.
            var blockchainSynced = await _blockchainService.SubmitHashToBlockchainAsync(
                log.Username ?? "Unknown",
                log.Action,
                log.Content,
                log.IpAddress);

            if (!blockchainSynced)
            {
                _logger.LogWarning(
                    "System log {LogId} was saved to SQL but failed to sync to blockchain.",
                    log.Id);
            }
        }
    }
}
