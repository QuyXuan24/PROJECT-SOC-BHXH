using System.Security.Claims;
using BHXH_Backend.Data;
using BHXH_Backend.Dtos;
using BHXH_Backend.Helpers;
using BHXH_Backend.Models;
using BHXH_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Employee,Staff")]
    public class StaffController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logger;
        private readonly BlockchainService _blockchainService;
        private readonly IConfiguration _config;

        public StaffController(
            ApplicationDbContext context,
            SystemLogService logger,
            BlockchainService blockchainService,
            IConfiguration config)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
            _config = config;
        }

        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications(
            [FromQuery] string? status,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var query = _context.BhxhRecords.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status.Trim());
            }

            if (from.HasValue)
            {
                query = query.Where(r => r.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(r => r.CreatedAt <= to.Value);
            }

            var aesKey = ConfigurationHelper.GetAesKey(_config);
            if (string.IsNullOrWhiteSpace(aesKey))
            {
                return StatusCode(500, new { message = "He thong chua cau hinh AesSettings:Key." });
            }

            var encryptedRecords = await query
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    r.FullName,
                    r.Cccd,
                    r.DateOfBirth,
                    r.Gender,
                    r.PhoneNumber,
                    r.BhxhCode,
                    r.Status,
                    r.ProcessedBy,
                    r.ReviewNote,
                    r.CreatedAt,
                    r.UpdatedAt
                })
                .ToListAsync();

            var records = encryptedRecords.Select(r => new
            {
                r.Id,
                r.UserId,
                r.FullName,
                Cccd = SecurityHelper.Decrypt(r.Cccd, aesKey),
                r.DateOfBirth,
                r.Gender,
                PhoneNumber = SecurityHelper.Decrypt(r.PhoneNumber, aesKey),
                BhxhCode = SecurityHelper.Decrypt(r.BhxhCode, aesKey),
                r.Status,
                r.ProcessedBy,
                r.ReviewNote,
                r.CreatedAt,
                r.UpdatedAt
            }).ToList();

            return Ok(records);
        }

        [HttpGet("pending-records")]
        public Task<IActionResult> GetPendingRecords()
        {
            return GetApplications("Pending", null, null);
        }

        [HttpPut("process-record/{id}")]
        public async Task<IActionResult> ProcessRecord(int id, [FromBody] ProcessRecordDto req)
        {
            var record = await _context.BhxhRecords.FindAsync(id);
            if (record == null)
            {
                return NotFound(new { message = "Ho so khong ton tai" });
            }

            if (record.Status == "Approved")
            {
                return BadRequest(new { message = "Ho so da duoc duyet, khong the thay doi trang thai." });
            }

            var note = req.Note?.Trim();
            if ((req.Action == "Rejected" || req.Action == "Cancelled") && string.IsNullOrWhiteSpace(note))
            {
                return BadRequest(new { message = "Khi từ chối hoặc khóa hồ sơ, vui lòng nhập lý do." });
            }

            var actor = User.FindFirst(ClaimTypes.Name)?.Value ?? "Employee";
            var actorIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

            record.Status = req.Action;
            record.ProcessedBy = actor;
            record.ReviewNote = note;
            record.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();

                var processMessage = BuildProcessRecordSnapshot(record, req.Action, actor);
                var recordKey = GetProcessRecordKey(record.Id);

                var blockchainSynced = await _blockchainService.SubmitHashToBlockchainAsync(
                    actor,
                    "PROCESS_RECORD",
                    processMessage,
                    actorIp,
                    recordKey);

                await _logger.WriteLogAsync(
                    actor,
                    "PROCESS_RECORD",
                    $"Nhan vien da {req.Action} ho so ID: {id}. Note={note}",
                    actorIp);

                string messageAction;
                if (req.Action == "Approved")
                    messageAction = "duyet";
                else if (req.Action == "Rejected")
                    messageAction = "tu choi";
                else if (req.Action == "Cancelled")
                    messageAction = "khoa";
                else
                    messageAction = "xu ly";

                return Ok(new
                {
                    message = $"Da {messageAction} ho so thanh cong!",
                    blockchainSynced
                });
            }
            catch (DbUpdateException ex)
            {
                await _logger.WriteLogAsync(
                    "System",
                    "DB_ERROR",
                    $"Loi cap nhat ho so {id}: {ex.Message}",
                    actorIp);

                return StatusCode(500, new { message = "Loi luu du lieu vao he thong." });
            }
        }

        [HttpGet("verify-record/{id}")]
        public async Task<IActionResult> VerifyProcessedRecord(int id)
        {
            var record = await _context.BhxhRecords.FindAsync(id);
            if (record == null)
            {
                return NotFound(new { message = "Ho so khong ton tai" });
            }

            var actor = User.FindFirst(ClaimTypes.Name)?.Value ?? "Employee";
            var actorIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
            var message = BuildProcessRecordSnapshot(record, record.Status, record.ProcessedBy ?? "Unknown");
            var recordKey = GetProcessRecordKey(record.Id);

            var verifyResult = await _blockchainService.VerifyHashOnBlockchainAsync(
                recordKey,
                actor,
                "PROCESS_RECORD",
                message,
                actorIp);

            return Ok(new
            {
                recordId = record.Id,
                recordKey,
                verified = verifyResult.verified,
                chainHash = verifyResult.chainHash,
                requestHash = verifyResult.requestHash
            });
        }

        private static string GetProcessRecordKey(int recordId) => $"PROCESS_RECORD:{recordId}";

        private static string BuildProcessRecordSnapshot(BhxhRecord record, string action, string processedBy)
        {
            var versionTicks = (record.UpdatedAt ?? record.CreatedAt).Ticks;
            return string.Join("|",
                $"RecordId={record.Id}",
                $"UserId={record.UserId}",
                $"Action={action}",
                $"ProcessedBy={processedBy}",
                $"VersionTicks={versionTicks}");
        }
    }
}
