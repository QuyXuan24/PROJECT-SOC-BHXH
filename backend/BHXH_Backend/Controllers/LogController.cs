using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BHXH_Backend.Data;
using BHXH_Backend.Services;
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
        [Authorize(Roles = "Admin, SOC")]
        public async Task<IActionResult> GetSystemLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var logs = await _context.SystemLogs
                    .OrderByDescending(l => l.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

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
    }

    public class LogRequestDto
    {
        public string? Username { get; set; }
        public required string Action { get; set; }
        public required string Content { get; set; }
    }
}
