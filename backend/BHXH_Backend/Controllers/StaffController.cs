using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BHXH_Backend.Data;
using BHXH_Backend.Dtos;
using BHXH_Backend.Models;
using BHXH_Backend.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BHXH_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class StaffController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logger;
        private readonly BlockchainService _blockchainService;

        public StaffController(
            ApplicationDbContext context,
            SystemLogService logger,
            BlockchainService blockchainService)
        {
            _context = context;
            _logger = logger;
            _blockchainService = blockchainService;
        }

        [HttpGet("pending-records")]
        public async Task<IActionResult> GetPendingRecords()
        {
            try
            {
                var records = await _context.BhxhRecords
                    .Where(r => r.Status == "Pending")
                    .ToListAsync();
                return Ok(records);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Loi ket noi database: " + ex.Message });
            }
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
                return BadRequest(new { message = "Ho so da duoc duyet, khong the thay doi trang thai!" });
            }

            var actor = User.FindFirst(ClaimTypes.Name)?.Value ?? "Staff";
            var actorIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

            record.Status = req.Action;
            record.ProcessedBy = actor;
            record.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();

                var processMessage = BuildProcessRecordSnapshot(record, req.Action, actor);
                var recordKey = GetProcessRecordKey(record.Id);

                // Tich hop blockchain truc tiep cho luong xu ly ho so.
                var blockchainSynced = await _blockchainService.SubmitHashToBlockchainAsync(
                    actor,
                    "PROCESS_RECORD",
                    processMessage,
                    actorIp,
                    recordKey);

                await _logger.WriteLogAsync(
                    actor,
                    "PROCESS_RECORD",
                    $"Nhan vien da {req.Action} ho so ID: {id}",
                    actorIp);

                var messageAction = req.Action == "Approved" ? "duyet" : "tu choi";
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

            var actor = User.FindFirst(ClaimTypes.Name)?.Value ?? "Staff";
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
