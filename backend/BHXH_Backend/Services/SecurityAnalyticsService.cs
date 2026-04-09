using BHXH_Backend.Data;
using BHXH_Backend.Dtos;
using BHXH_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Services
{
    public class SecurityAnalyticsService
    {
        private static readonly string[] SqlInjectionKeywords =
        {
            "union select",
            "' or 1=1",
            "drop table",
            "xp_cmdshell",
            "information_schema",
            "sleep(",
            "benchmark(",
            " or 1=1",
            "--"
        };

        private readonly ApplicationDbContext _context;
        private readonly ILogger<SecurityAnalyticsService> _logger;

        public SecurityAnalyticsService(ApplicationDbContext context, ILogger<SecurityAnalyticsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SecurityOverviewDto> GetOverviewAsync(bool detailedLoggingEnabled, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var from = now.AddHours(-24);

            var logs = await _context.SystemLogs
                .AsNoTracking()
                .Where(l => l.CreatedAt >= from)
                .Select(l => new { l.Id, l.Action, l.Content, l.IpAddress, l.CreatedAt })
                .ToListAsync(cancellationToken);

            var activeBlockedIps = await _context.BlockedIps
                .AsNoTracking()
                .CountAsync(b => b.IsActive && (!b.ExpiresAt.HasValue || b.ExpiresAt > now), cancellationToken);

            var openIncidents = await _context.Incidents
                .AsNoTracking()
                .CountAsync(i => i.Status == "Open" || i.Status == "InProgress", cancellationToken);

            var alerts = await GetRealtimeAlertsAsync(60, cancellationToken);

            var attackByType = logs
                .GroupBy(l => ClassifyThreatType(l.Action, l.Content))
                .OrderByDescending(g => g.Count())
                .Take(6)
                .Select(g => new AttackCategoryDto
                {
                    Type = g.Key,
                    Count = g.Count()
                })
                .ToList();

            var trend = logs
                .GroupBy(l => new DateTime(l.CreatedAt.Year, l.CreatedAt.Month, l.CreatedAt.Day, l.CreatedAt.Hour, 0, 0))
                .OrderBy(g => g.Key)
                .Select(g => new LogTrendPointDto
                {
                    Label = g.Key.ToLocalTime().ToString("HH:mm"),
                    Count = g.Count()
                })
                .TakeLast(24)
                .ToList();

            var topIps = logs
                .Where(l => !string.IsNullOrWhiteSpace(l.IpAddress))
                .GroupBy(l => l.IpAddress!)
                .OrderByDescending(g => g.Count())
                .Take(8)
                .Select(g => new TopSourceIpDto
                {
                    IpAddress = g.Key,
                    Count = g.Count()
                })
                .ToList();

            return new SecurityOverviewDto
            {
                GeneratedAt = now,
                TotalLogs24h = logs.Count,
                FailedLogins24h = logs.Count(l => IsLoginFailed(l.Action, l.Content)),
                ActiveBlockedIps = activeBlockedIps,
                OpenIncidents = openIncidents,
                CriticalAlerts = alerts.Count(a => IsCriticalSeverity(a.Severity)),
                DetailedLoggingEnabled = detailedLoggingEnabled,
                AttackByType = attackByType,
                LogTrend = trend,
                TopSourceIps = topIps
            };
        }

        public async Task<List<SecurityAlertDto>> GetRealtimeAlertsAsync(int minutes = 60, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var from = now.AddMinutes(-Math.Clamp(minutes, 5, 240));

            var logs = await _context.SystemLogs
                .AsNoTracking()
                .Where(l => l.CreatedAt >= from)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new { l.Id, l.Action, l.Content, l.Username, l.IpAddress, l.CreatedAt })
                .ToListAsync(cancellationToken);

            var alerts = new List<SecurityAlertDto>();

            var bruteForceGroups = logs
                .Where(l => IsLoginFailed(l.Action, l.Content))
                .Where(l => !string.IsNullOrWhiteSpace(l.IpAddress))
                .GroupBy(l => l.IpAddress!)
                .Where(g => g.Count() >= 5)
                .OrderByDescending(g => g.Count());

            foreach (var group in bruteForceGroups)
            {
                var latest = group.OrderByDescending(x => x.CreatedAt).First();
                alerts.Add(new SecurityAlertDto
                {
                    AlertId = $"BRUTE-{group.Key}-{latest.Id}",
                    Severity = group.Count() >= 10 ? "Critical" : "High",
                    Category = "BruteForce",
                    Title = "Nghi van tan cong dang nhap",
                    Description = $"IP {group.Key} co {group.Count()} lan dang nhap that bai trong {minutes} phut.",
                    SourceIp = group.Key,
                    Username = latest.Username,
                    RelatedLogId = latest.Id,
                    CreatedAt = latest.CreatedAt,
                    RecommendedAction = "Block IP + Khoa tai khoan + Tao incident",
                    Status = "Open"
                });
            }

            var sqlInjectionLogs = logs
                .Where(l => ContainsSqlInjection(l.Action) || ContainsSqlInjection(l.Content))
                .Take(20);

            foreach (var log in sqlInjectionLogs)
            {
                alerts.Add(new SecurityAlertDto
                {
                    AlertId = $"SQLI-{log.Id}",
                    Severity = "High",
                    Category = "SqlInjection",
                    Title = "Phat hien mau SQL Injection",
                    Description = log.Content ?? string.Empty,
                    SourceIp = log.IpAddress,
                    Username = log.Username,
                    RelatedLogId = log.Id,
                    CreatedAt = log.CreatedAt,
                    RecommendedAction = "Block IP + Tao incident",
                    Status = "Open"
                });
            }

            var accountLockLogs = logs
                .Where(l => (l.Action ?? string.Empty).Contains("LOCK", StringComparison.OrdinalIgnoreCase))
                .Take(20);

            foreach (var log in accountLockLogs)
            {
                alerts.Add(new SecurityAlertDto
                {
                    AlertId = $"LOCK-{log.Id}",
                    Severity = "Medium",
                    Category = "AccountLock",
                    Title = "Su kien lien quan khoa tai khoan",
                    Description = log.Content ?? string.Empty,
                    SourceIp = log.IpAddress,
                    Username = log.Username,
                    RelatedLogId = log.Id,
                    CreatedAt = log.CreatedAt,
                    RecommendedAction = "Xem chi tiet va tao incident neu can",
                    Status = "Open"
                });
            }

            var openIncidents = await _context.Incidents
                .AsNoTracking()
                .Where(i => (i.Status == "Open" || i.Status == "InProgress") && i.CreatedAt >= from)
                .OrderByDescending(i => i.CreatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);

            alerts.AddRange(openIncidents.Select(i => new SecurityAlertDto
            {
                AlertId = $"INC-{i.Id}",
                Severity = NormalizeSeverity(i.Severity),
                Category = i.Category ?? "Incident",
                Title = i.Title,
                Description = i.Description,
                SourceIp = i.SourceIp,
                Username = i.Username,
                RelatedLogId = i.RelatedLogId,
                CreatedAt = i.CreatedAt,
                RecommendedAction = "Theo doi xu ly incident",
                Status = i.Status
            }));

            return alerts
                .OrderByDescending(a => SeverityRank(a.Severity))
                .ThenByDescending(a => a.CreatedAt)
                .Take(100)
                .ToList();
        }

        public async Task ProcessLogAsync(SystemLog log, CancellationToken cancellationToken = default)
        {
            try
            {
                if (IsLoginFailed(log) && !string.IsNullOrWhiteSpace(log.IpAddress))
                {
                    var thresholdTime = DateTime.UtcNow.AddMinutes(-10);
                    var recentLogs = await _context.SystemLogs
                        .AsNoTracking()
                        .Where(l => l.CreatedAt >= thresholdTime && l.IpAddress == log.IpAddress)
                        .Select(l => new { l.Action, l.Content })
                        .ToListAsync(cancellationToken);

                    var failCount = recentLogs.Count(l => IsLoginFailed(l.Action, l.Content));

                    if (failCount >= 5)
                    {
                        await CreateAutoIncidentIfNeededAsync(
                            title: "Tu dong phat hien brute-force login",
                            category: "BruteForce",
                            severity: failCount >= 10 ? "Critical" : "High",
                            description: $"IP {log.IpAddress} co {failCount} lan dang nhap that bai trong 10 phut.",
                            sourceIp: log.IpAddress,
                            username: log.Username,
                            relatedLogId: log.Id,
                            cancellationToken: cancellationToken);
                    }
                }

                if (ContainsSqlInjection(log.Content) || ContainsSqlInjection(log.Action))
                {
                    await CreateAutoIncidentIfNeededAsync(
                        title: "Tu dong phat hien SQL Injection",
                        category: "SqlInjection",
                        severity: "High",
                        description: log.Content ?? string.Empty,
                        sourceIp: log.IpAddress,
                        username: log.Username,
                        relatedLogId: log.Id,
                        cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Khong the xu ly auto-detect cho log {LogId}", log.Id);
            }
        }

        private async Task CreateAutoIncidentIfNeededAsync(
            string title,
            string category,
            string severity,
            string description,
            string? sourceIp,
            string? username,
            int? relatedLogId,
            CancellationToken cancellationToken)
        {
            var windowStart = DateTime.UtcNow.AddMinutes(-30);
            var exists = await _context.Incidents
                .AsNoTracking()
                .AnyAsync(i =>
                    i.Category == category &&
                    i.SourceIp == sourceIp &&
                    (i.Status == "Open" || i.Status == "InProgress") &&
                    i.CreatedAt >= windowStart,
                    cancellationToken);

            if (exists)
            {
                return;
            }

            var incident = new Incident
            {
                IncidentCode = $"INC-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                Title = title,
                Severity = NormalizeSeverity(severity),
                Status = "Open",
                Category = category,
                SourceIp = sourceIp,
                Username = username,
                RelatedLogId = relatedLogId,
                Description = string.IsNullOrWhiteSpace(description) ? title : description,
                CreatedBy = "SOC-AUTO",
                CreatedAt = DateTime.UtcNow
            };

            _context.Incidents.Add(incident);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static string ClassifyThreatType(string? actionValue, string? contentValue)
        {
            var action = (actionValue ?? string.Empty).ToUpperInvariant();
            var content = (contentValue ?? string.Empty).ToLowerInvariant();

            if (action.Contains("LOGIN_FAILED") || content.Contains("dang nhap that bai")) return "Dang nhap that bai";
            if (action.Contains("LOCK") || content.Contains("khoa tai khoan")) return "Khoa tai khoan";
            if (ContainsSqlInjection(content) || ContainsSqlInjection(action)) return "SQL Injection";
            if (action.Contains("FORBIDDEN") || action.Contains("UNAUTHORIZED")) return "Truy cap trai phep";
            if (action.Contains("SYSTEM_ERROR")) return "Loi he thong";
            return "Hoat dong khac";
        }

        private static bool IsLoginFailed(SystemLog log)
        {
            return IsLoginFailed(log.Action, log.Content);
        }

        private static bool IsLoginFailed(string? actionValue, string? contentValue)
        {
            var action = (actionValue ?? string.Empty).ToUpperInvariant();
            var content = (contentValue ?? string.Empty).ToLowerInvariant();
            return action.Contains("LOGIN_FAILED") || content.Contains("sai mat khau") || content.Contains("dang nhap that bai");
        }

        private static bool ContainsSqlInjection(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.ToLowerInvariant();
            return SqlInjectionKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
        }

        private static string NormalizeSeverity(string severity)
        {
            var normalized = (severity ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "critical" => "Critical",
                "high" => "High",
                "medium" => "Medium",
                "low" => "Low",
                _ => "Medium"
            };
        }

        private static bool IsCriticalSeverity(string severity)
        {
            var normalized = NormalizeSeverity(severity);
            return normalized == "Critical" || normalized == "High";
        }

        private static int SeverityRank(string severity)
        {
            return NormalizeSeverity(severity) switch
            {
                "Critical" => 4,
                "High" => 3,
                "Medium" => 2,
                "Low" => 1,
                _ => 0
            };
        }
    }
}
