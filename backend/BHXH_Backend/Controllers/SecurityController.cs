using BHXH_Backend.Data;
using BHXH_Backend.Dtos;
using BHXH_Backend.Models;
using BHXH_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Controllers
{
    [Route("api/security")]
    [ApiController]
    [Authorize(Roles = "Admin,Security,SOC")]
    public class SecurityController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SecurityAnalyticsService _securityAnalyticsService;
        private readonly SystemLogService _logService;

        public SecurityController(
            ApplicationDbContext context,
            SecurityAnalyticsService securityAnalyticsService,
            SystemLogService logService)
        {
            _context = context;
            _securityAnalyticsService = securityAnalyticsService;
            _logService = logService;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
        {
            var overview = await _securityAnalyticsService.GetOverviewAsync(_logService.IsDetailedLoggingEnabled, cancellationToken);
            return Ok(overview);
        }

        [HttpGet("alerts/realtime")]
        public async Task<IActionResult> GetRealtimeAlerts([FromQuery] int minutes = 60, CancellationToken cancellationToken = default)
        {
            var alerts = await _securityAnalyticsService.GetRealtimeAlertsAsync(minutes, cancellationToken);
            return Ok(alerts);
        }

        [HttpGet("blocked-ips")]
        public async Task<IActionResult> GetBlockedIps(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var data = await _context.BlockedIps
                .AsNoTracking()
                .Where(b => b.IsActive && (!b.ExpiresAt.HasValue || b.ExpiresAt > now))
                .OrderByDescending(b => b.CreatedAt)
                .Take(200)
                .ToListAsync(cancellationToken);

            return Ok(data);
        }

        [HttpPost("block-ip")]
        public async Task<IActionResult> BlockIp([FromBody] BlockIpRequestDto request, CancellationToken cancellationToken)
        {
            var ipAddress = (request.IpAddress ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return BadRequest(new { message = "IP khong hop le." });
            }

            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Chan thu cong tu SOC dashboard"
                : request.Reason.Trim();

            var actor = User.Identity?.Name ?? "Security";
            var now = DateTime.UtcNow;
            var expiresAt = request.DurationMinutes.HasValue && request.DurationMinutes.Value > 0
                ? now.AddMinutes(request.DurationMinutes.Value)
                : (DateTime?)null;

            var existing = await _context.BlockedIps
                .FirstOrDefaultAsync(b => b.IpAddress == ipAddress && b.IsActive, cancellationToken);

            if (existing != null)
            {
                existing.Reason = reason;
                existing.BlockedBy = actor;
                existing.CreatedAt = now;
                existing.ExpiresAt = expiresAt;
                existing.LastHitAt = now;
            }
            else
            {
                _context.BlockedIps.Add(new BlockedIp
                {
                    IpAddress = ipAddress,
                    Reason = reason,
                    BlockedBy = actor,
                    CreatedAt = now,
                    ExpiresAt = expiresAt,
                    IsActive = true
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            await _logService.WriteLogAsync(
                actor,
                "SECURITY_BLOCK_IP",
                $"SOC da chan IP {ipAddress}. Ly do: {reason}",
                GetClientIpAddress());

            return Ok(new
            {
                message = $"Da chan IP {ipAddress}.",
                ipAddress,
                expiresAt
            });
        }

        [HttpPost("lock-account")]
        public async Task<IActionResult> LockAccount([FromBody] LockAccountRequestDto request, CancellationToken cancellationToken)
        {
            if (request.UserId is null && string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest(new { message = "Can cung cap username hoac userId." });
            }

            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Khoa thu cong tu SOC dashboard"
                : request.Reason.Trim();

            User? user = null;
            if (request.UserId.HasValue)
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId.Value, cancellationToken);
            }

            if (user == null && !string.IsNullOrWhiteSpace(request.Username))
            {
                var normalizedUsername = request.Username.Trim();
                user = await _context.Users.FirstOrDefaultAsync(u => u.Username == normalizedUsername, cancellationToken);
            }

            if (user == null)
            {
                return NotFound(new { message = "Khong tim thay tai khoan." });
            }

            user.IsLocked = true;
            user.LockoutEnd = DateTime.UtcNow.AddYears(100);
            user.FailedLoginAttempts = Math.Max(user.FailedLoginAttempts, 5);
            await _context.SaveChangesAsync(cancellationToken);

            var actor = User.Identity?.Name ?? "Security";
            await _logService.WriteLogAsync(
                actor,
                "SECURITY_LOCK_ACCOUNT",
                $"SOC da khoa tai khoan {user.Username}. Ly do: {reason}",
                GetClientIpAddress());

            return Ok(new
            {
                message = $"Da khoa tai khoan {user.Username}.",
                userId = user.Id,
                user.Username,
                user.IsLocked
            });
        }

        private string GetClientIpAddress()
        {
            var xForwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xForwardedFor))
            {
                return xForwardedFor.Split(',').First().Trim();
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
        }
    }
}
