using BHXH_Backend.Data;
using BHXH_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logService;

        public LogController(ApplicationDbContext context, SystemLogService logService)
        {
            _context = context;
            _logService = logService;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Security,SOC")]
        public async Task<IActionResult> GetSystemLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? action = null,
            [FromQuery] string? severity = null,
            [FromQuery] string? ipAddress = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] bool includeTotal = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 500);

                var query = _context.SystemLogs.AsNoTracking().AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var keyword = search.Trim();
                    query = query.Where(l =>
                        (l.Action != null && l.Action.Contains(keyword)) ||
                        (l.Content != null && l.Content.Contains(keyword)) ||
                        (l.Username != null && l.Username.Contains(keyword)) ||
                        (l.IpAddress != null && l.IpAddress.Contains(keyword)));
                }

                if (!string.IsNullOrWhiteSpace(action))
                {
                    var actionFilter = action.Trim();
                    query = query.Where(l => l.Action != null && l.Action.Contains(actionFilter));
                }

                if (!string.IsNullOrWhiteSpace(ipAddress))
                {
                    var ipFilter = ipAddress.Trim();
                    query = query.Where(l => l.IpAddress != null && l.IpAddress.Contains(ipFilter));
                }

                if (from.HasValue)
                {
                    query = query.Where(l => l.CreatedAt >= from.Value);
                }

                if (to.HasValue)
                {
                    query = query.Where(l => l.CreatedAt <= to.Value);
                }

                if (!string.IsNullOrWhiteSpace(severity))
                {
                    var sev = severity.Trim().ToLowerInvariant();
                    query = sev switch
                    {
                        "critical" => query.Where(l => l.Action.Contains("ERROR") || l.Action.Contains("UNAUTHORIZED") || l.Action.Contains("FORBIDDEN")),
                        "high" => query.Where(l => l.Action.Contains("FAILED") || l.Action.Contains("LOCK") || l.Action.Contains("BLOCK")),
                        "medium" => query.Where(l => l.Action.Contains("MODE") || l.Action.Contains("PROCESS")),
                        "low" => query.Where(l => l.Action.Contains("SUCCESS") || l.Action.Contains("VIEW") || l.Action.Contains("GET")),
                        _ => query
                    };
                }

                var total = includeTotal ? await query.CountAsync(cancellationToken) : 0;

                var logs = await query
                    .OrderByDescending(l => l.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);

                if (includeTotal)
                {
                    return Ok(new
                    {
                        items = logs,
                        total,
                        page,
                        pageSize
                    });
                }

                return Ok(logs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Loi he thong khi lay log", error = ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> WriteLog([FromBody] LogRequestDto request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

            await _logService.WriteLogAsync(
                request.Username ?? User.Identity?.Name ?? "He thong",
                request.Action,
                request.Content,
                ipAddress);

            return Ok(new { message = "Da ghi log thanh cong!" });
        }

        [HttpGet("mode")]
        [Authorize(Roles = "Admin,Security,SOC")]
        public IActionResult GetLoggingMode()
        {
            return Ok(new { detailedLoggingEnabled = _logService.IsDetailedLoggingEnabled });
        }

        [HttpPut("mode")]
        [Authorize(Roles = "Security,SOC")]
        public async Task<IActionResult> SetLoggingMode([FromBody] SetLogModeRequest request)
        {
            var actor = User.Identity?.Name ?? "Security";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

            await _logService.SetDetailedLoggingModeAsync(actor, request.Enabled, ipAddress);
            return Ok(new
            {
                message = "Da cap nhat che do ghi log.",
                detailedLoggingEnabled = request.Enabled
            });
        }
    }

    public class LogRequestDto
    {
        public string? Username { get; set; }
        public required string Action { get; set; }
        public required string Content { get; set; }
    }

    public class SetLogModeRequest
    {
        public bool Enabled { get; set; }
    }
}
