using BHXH_Backend.Data;
using BHXH_Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Middlewares
{
    public class BlockedIpMiddleware
    {
        private readonly RequestDelegate _next;

        public BlockedIpMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, SystemLogService logService)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var ip = ExtractClientIp(context);
            if (string.IsNullOrWhiteSpace(ip) || ip == "Unknown IP")
            {
                await _next(context);
                return;
            }

            var now = DateTime.UtcNow;
            var blocked = await dbContext.BlockedIps
                .FirstOrDefaultAsync(b => b.IpAddress == ip && b.IsActive, context.RequestAborted);

            if (blocked == null)
            {
                await _next(context);
                return;
            }

            if (blocked.ExpiresAt.HasValue && blocked.ExpiresAt.Value <= now)
            {
                blocked.IsActive = false;
                blocked.LastHitAt = now;
                await dbContext.SaveChangesAsync(context.RequestAborted);
                await _next(context);
                return;
            }

            blocked.LastHitAt = now;
            await dbContext.SaveChangesAsync(context.RequestAborted);

            await logService.WriteLogAsync(
                context.User.Identity?.Name ?? "Unknown",
                "BLOCKED_IP_REQUEST",
                $"Tu choi request tu IP {ip} den {path}",
                ip);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                message = "IP cua ban dang bi chan boi he thong SOC.",
                ip,
                blockedUntil = blocked.ExpiresAt
            });
        }

        private static string ExtractClientIp(HttpContext context)
        {
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xForwardedFor))
            {
                return xForwardedFor.Split(',').First().Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
        }
    }
}
