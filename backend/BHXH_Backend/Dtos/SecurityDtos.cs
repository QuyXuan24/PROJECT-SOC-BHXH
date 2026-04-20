namespace BHXH_Backend.Dtos
{
    public class SecurityOverviewDto
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalLogs24h { get; set; }
        public int FailedLogins24h { get; set; }
        public int ActiveBlockedIps { get; set; }
        public int OpenIncidents { get; set; }
        public int CriticalAlerts { get; set; }
        public bool DetailedLoggingEnabled { get; set; }
        public List<AttackCategoryDto> AttackByType { get; set; } = new();
        public List<LogTrendPointDto> LogTrend { get; set; } = new();
        public List<TopSourceIpDto> TopSourceIps { get; set; } = new();
    }

    public class AttackCategoryDto
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class LogTrendPointDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class TopSourceIpDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class SecurityAlertDto
    {
        public string AlertId { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium";
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? SourceIp { get; set; }
        public string? Username { get; set; }
        public int? RelatedLogId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
    }

    public class BlockIpRequestDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public int? DurationMinutes { get; set; }
    }

    public class LockAccountRequestDto
    {
        public string? Username { get; set; }
        public int? UserId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class CreateIncidentRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium";
        public string Description { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? SourceIp { get; set; }
        public string? Username { get; set; }
        public int? RelatedLogId { get; set; }
    }
}
