using BHXH_Backend.Data;
using BHXH_Backend.Dtos;
using BHXH_Backend.Models;
using BHXH_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Security,SOC")]
    public class IncidentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logService;

        public IncidentsController(ApplicationDbContext context, SystemLogService logService)
        {
            _context = context;
            _logService = logService;
        }

        [HttpGet]
        public async Task<IActionResult> GetIncidents([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _context.Incidents.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(i => i.Status == status);
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                items,
                total,
                page,
                pageSize
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetIncidentById(int id, CancellationToken cancellationToken = default)
        {
            var incident = await _context.Incidents.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
            if (incident == null)
            {
                return NotFound(new { message = "Khong tim thay incident." });
            }

            return Ok(incident);
        }

        [HttpPost]
        public async Task<IActionResult> CreateIncident([FromBody] CreateIncidentRequestDto request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(new { message = "Title va description la bat buoc." });
            }

            var actor = User.Identity?.Name ?? "Security";
            var incident = new Incident
            {
                IncidentCode = $"INC-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Severity = NormalizeSeverity(request.Severity),
                Status = "Open",
                Category = string.IsNullOrWhiteSpace(request.Category) ? "Manual" : request.Category.Trim(),
                SourceIp = string.IsNullOrWhiteSpace(request.SourceIp) ? null : request.SourceIp.Trim(),
                Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim(),
                RelatedLogId = request.RelatedLogId,
                CreatedBy = actor,
                CreatedAt = DateTime.UtcNow
            };

            _context.Incidents.Add(incident);
            await _context.SaveChangesAsync(cancellationToken);

            await _logService.WriteLogAsync(
                actor,
                "CREATE_INCIDENT",
                $"Tao incident {incident.IncidentCode} ({incident.Title})",
                GetClientIpAddress());

            return Ok(new
            {
                message = "Da tao incident thanh cong.",
                incidentId = incident.Id,
                incident.IncidentCode,
                incident.Status
            });
        }

        [HttpPut("{id:int}/status")]
        public async Task<IActionResult> UpdateIncidentStatus(int id, [FromBody] UpdateIncidentStatusRequest request, CancellationToken cancellationToken = default)
        {
            var incident = await _context.Incidents.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
            if (incident == null)
            {
                return NotFound(new { message = "Khong tim thay incident." });
            }

            incident.Status = NormalizeStatus(request.Status);
            incident.ResolutionNote = string.IsNullOrWhiteSpace(request.ResolutionNote) ? incident.ResolutionNote : request.ResolutionNote.Trim();
            incident.UpdatedAt = DateTime.UtcNow;
            if (incident.Status == "Resolved" || incident.Status == "Closed")
            {
                incident.ResolvedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            await _logService.WriteLogAsync(
                User.Identity?.Name,
                "UPDATE_INCIDENT_STATUS",
                $"Cap nhat incident {incident.IncidentCode} sang trang thai {incident.Status}",
                GetClientIpAddress());

            return Ok(new
            {
                message = "Da cap nhat trang thai incident.",
                incident.Id,
                incident.Status
            });
        }

        private static string NormalizeSeverity(string? severity)
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

        private static string NormalizeStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "open" => "Open",
                "inprogress" => "InProgress",
                "resolved" => "Resolved",
                "closed" => "Closed",
                _ => "Open"
            };
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

    public class UpdateIncidentStatusRequest
    {
        public string Status { get; set; } = "Open";
        public string? ResolutionNote { get; set; }
    }
}
