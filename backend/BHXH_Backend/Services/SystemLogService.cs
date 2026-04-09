using BHXH_Backend.Data;
using BHXH_Backend.Models;

namespace BHXH_Backend.Services
{
    public class SystemLogService
    {
        private static volatile bool _detailedLoggingEnabled = true;
        private static readonly HashSet<string> CriticalActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "LOGIN_SUCCESS",
            "LOGIN_FAILED",
            "LOGIN_BLOCKED",
            "ACCOUNT_LOCKED",
            "LOCK_USER",
            "UNLOCK_USER",
            "CREATE_USER",
            "UPDATE_USER",
            "LOGGING_MODE_CHANGED"
        };

        private readonly ApplicationDbContext _context;
        private readonly BlockchainService _blockchainService;
        private readonly SecurityAnalyticsService _securityAnalyticsService;
        private readonly ILogger<SystemLogService> _logger;

        public SystemLogService(
            ApplicationDbContext context,
            BlockchainService blockchainService,
            SecurityAnalyticsService securityAnalyticsService,
            ILogger<SystemLogService> logger)
        {
            _context = context;
            _blockchainService = blockchainService;
            _securityAnalyticsService = securityAnalyticsService;
            _logger = logger;
        }

        public bool IsDetailedLoggingEnabled => _detailedLoggingEnabled;

        public async Task SetDetailedLoggingModeAsync(string actor, bool enabled, string? ipAddress = null)
        {
            _detailedLoggingEnabled = enabled;
            await WriteLogAsync(actor, "LOGGING_MODE_CHANGED", $"Detailed logging set to: {enabled}", ipAddress);
        }

        // Ham nay se duoc goi o bat ky dau can ghi log.
        // Log van luu SQL ngay ca khi bridge blockchain tam thoi loi.
        public async Task WriteLogAsync(
            string? username,
            string action,
            string content,
            string? ipAddress = null)
        {
            if (!_detailedLoggingEnabled && !CriticalActions.Contains(action))
            {
                return;
            }

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

            await _securityAnalyticsService.ProcessLogAsync(log);

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
