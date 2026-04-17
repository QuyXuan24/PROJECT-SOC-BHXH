using System.Security.Claims;
using BHXH_Backend.Data;
using BHXH_Backend.Helpers;
using BHXH_Backend.Models;
using BHXH_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Controllers
{
    public class UserProfileDto
    {
        public required string FullName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public required string Gender { get; set; }

        public required string Cccd { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Address { get; set; }
        public required string BhxhCode { get; set; }
        public string? CompanyName { get; set; }
        public decimal? Salary { get; set; }
    }

    public class CancelApplicationDto
    {
        public required string Reason { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "User")]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly SystemLogService _logService;
        private readonly BlockchainService _blockchainService;

        public UserController(
            ApplicationDbContext context,
            IConfiguration config,
            SystemLogService logService,
            BlockchainService blockchainService)
        {
            _context = context;
            _config = config;
            _logService = logService;
            _blockchainService = blockchainService;
        }

        [HttpPost("profile")]
        public async Task<IActionResult> SubmitProfile([FromBody] UserProfileDto req)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Cccd) || string.IsNullOrWhiteSpace(req.BhxhCode))
            {
                return BadRequest(new { message = "Thong tin ho so khong hop le." });
            }
            if (req.Salary.HasValue && req.Salary.Value < 0)
            {
                return BadRequest(new { message = "Luong phai >= 0." });
            }

            var actor = User.Identity?.Name ?? "Unknown";
            var actorIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

            var record = await _context.BhxhRecords.FirstOrDefaultAsync(r => r.UserId == userId);
            var aesKey = ConfigurationHelper.GetAesKey(_config);
            if (string.IsNullOrWhiteSpace(aesKey))
            {
                return StatusCode(500, new { message = "He thong chua cau hinh AesSettings:Key." });
            }

            var isCreate = record == null;

            if (isCreate)
            {
                record = new BhxhRecord
                {
                    UserId = userId,
                    FullName = req.FullName,
                    DateOfBirth = req.DateOfBirth,
                    Gender = req.Gender,
                    Cccd = SecurityHelper.Encrypt(req.Cccd, aesKey),
                    PhoneNumber = SecurityHelper.Encrypt(req.PhoneNumber, aesKey),
                    Address = SecurityHelper.Encrypt(req.Address, aesKey),
                    BhxhCode = SecurityHelper.Encrypt(req.BhxhCode, aesKey),
                    CccdHash = HashHelper.ToSha256(req.Cccd),
                    BhxhCodeHash = HashHelper.ToSha256(req.BhxhCode),
                    CompanyName = string.IsNullOrWhiteSpace(req.CompanyName) ? null : req.CompanyName.Trim(),
                    Salary = req.Salary,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };
                _context.BhxhRecords.Add(record);
            }
            else
            {
                if (record!.Status == "Approved")
                {
                    return BadRequest("Ho so da duoc duyet, ban khong the tu sua nua!");
                }

                record.FullName = req.FullName;
                record.DateOfBirth = req.DateOfBirth;
                record.Gender = req.Gender;
                record.Cccd = SecurityHelper.Encrypt(req.Cccd, aesKey);
                record.PhoneNumber = SecurityHelper.Encrypt(req.PhoneNumber, aesKey);
                record.Address = SecurityHelper.Encrypt(req.Address, aesKey);
                record.BhxhCode = SecurityHelper.Encrypt(req.BhxhCode, aesKey);
                record.CccdHash = HashHelper.ToSha256(req.Cccd);
                record.BhxhCodeHash = HashHelper.ToSha256(req.BhxhCode);
                if (!string.IsNullOrWhiteSpace(req.CompanyName))
                {
                    record.CompanyName = req.CompanyName.Trim();
                }
                if (req.Salary.HasValue)
                {
                    record.Salary = req.Salary.Value;
                }
                record.Status = "Pending";
                record.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var action = isCreate ? "SUBMIT_PROFILE" : "UPDATE_PROFILE";
            var blockchainMessage = BuildEncryptedProfileSnapshot(record!);
            var recordKey = GetProfileRecordKey(record!.Id);

            var blockchainSynced = await _blockchainService.SubmitHashToBlockchainAsync(
                actor,
                action,
                blockchainMessage,
                actorIp,
                recordKey);

            await _logService.WriteLogAsync(
                actor,
                action,
                $"Nguoi dung da {(isCreate ? "nop" : "cap nhat")} ho so ID: {record.Id}",
                actorIp);

            return Ok(new
            {
                message = "Nop ho so thanh cong! Vui long cho nhan vien xet duyet.",
                recordId = record.Id,
                blockchainSynced
            });
        }

        [HttpPost("applications/{id}/cancel")]
        public async Task<IActionResult> CancelPendingApplication(int id, [FromBody] CancelApplicationDto request)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length < 10)
            {
                return BadRequest(new { message = "Ly do huy phai toi thieu 10 ky tu." });
            }

            var record = await _context.BhxhRecords.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (record == null)
            {
                return NotFound(new { message = "Khong tim thay ho so." });
            }

            if (!string.Equals(record.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Chi duoc huy ho so o trang thai cho duyet." });
            }

            record.Status = "Cancelled";
            record.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _logService.WriteLogAsync(User.Identity?.Name, "CANCEL_APPLICATION", $"Nguoi dung huy ho so ID: {record.Id}. Ly do: {reason}");
            return Ok(new { message = "Da huy ho so thanh cong." });
        }

        [HttpGet("applications")]
        public async Task<IActionResult> GetMyApplications()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var records = await _context.BhxhRecords
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Status,
                    submittedAt = r.CreatedAt,
                    updatedAt = r.UpdatedAt
                })
                .ToListAsync();

            var result = new List<object>();
            foreach (var record in records)
            {
                string? failureReason = null;
                if (record.Status == "Rejected")
                {
                    failureReason = await _context.SystemLogs
                        .Where(l => l.Action == "PROCESS_RECORD" && l.Content.Contains($"ID: {record.Id}") && l.Content.Contains("Rejected"))
                        .OrderByDescending(l => l.CreatedAt)
                        .Select(l => l.Content)
                        .FirstOrDefaultAsync();
                }

                result.Add(new
                {
                    record.Id,
                    record.Status,
                    record.submittedAt,
                    record.updatedAt,
                    failureReason
                });
            }

            return Ok(result);
        }

        [HttpGet("applications/{id}/timeline")]
        public async Task<IActionResult> GetApplicationTimeline(int id)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var record = await _context.BhxhRecords.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (record == null)
            {
                return NotFound(new { message = "Khong tim thay ho so." });
            }

            var timeline = await _context.SystemLogs
                .Where(l => l.Content.Contains($"ID: {id}"))
                .OrderBy(l => l.CreatedAt)
                .Select(l => new
                {
                    l.CreatedAt,
                    l.Username,
                    l.Action,
                    l.Content
                })
                .ToListAsync();

            return Ok(timeline);
        }

        [HttpGet("profile/verify")]
        public async Task<IActionResult> VerifyMyProfileOnBlockchain()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var actor = User.Identity?.Name ?? "Unknown";
            var actorIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

            var record = await _context.BhxhRecords.FirstOrDefaultAsync(r => r.UserId == userId);
            if (record == null)
            {
                return NotFound("Ban chua nop ho so nao.");
            }

            var snapshot = BuildEncryptedProfileSnapshot(record);
            var recordKey = GetProfileRecordKey(record.Id);
            var verifyResult = await _blockchainService.VerifyHashOnBlockchainAsync(
                recordKey,
                actor,
                "PROFILE_STATE",
                snapshot,
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

        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var record = await _context.BhxhRecords.FirstOrDefaultAsync(r => r.UserId == userId);
            if (record == null)
            {
                return NotFound("Ban chua nop ho so nao.");
            }

            var aesKey = ConfigurationHelper.GetAesKey(_config);
            if (string.IsNullOrWhiteSpace(aesKey))
            {
                return StatusCode(500, new { message = "He thong chua cau hinh AesSettings:Key." });
            }

            string? latestProcessNote = null;
            if (record.Status == "Rejected" || record.Status == "Cancelled")
            {
                latestProcessNote = await _context.SystemLogs
                    .Where(l => l.Content.Contains($"ID: {record.Id}") && (l.Action == "PROCESS_RECORD" || l.Action == "CANCEL_APPLICATION"))
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => l.Content)
                    .FirstOrDefaultAsync();
            }

            return Ok(new
            {
                RecordId = record.Id,
                Status = record.Status,
                Note = latestProcessNote,
                FullName = record.FullName,
                DateOfBirth = record.DateOfBirth.ToString("dd/MM/yyyy"),
                Gender = record.Gender,
                Cccd = SecurityHelper.Decrypt(record.Cccd, aesKey),
                PhoneNumber = SecurityHelper.Decrypt(record.PhoneNumber, aesKey),
                Address = SecurityHelper.Decrypt(record.Address, aesKey),
                BhxhCode = SecurityHelper.Decrypt(record.BhxhCode, aesKey),
                CompanyName = record.CompanyName,
                Salary = record.Salary
            });
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            var userIdStr =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("id");

            return int.TryParse(userIdStr, out userId);
        }

        private static string GetProfileRecordKey(int recordId) => $"PROFILE_RECORD:{recordId}";

        private static string BuildEncryptedProfileSnapshot(BhxhRecord record)
        {
            var profileDate = record.DateOfBirth.Date.ToString("yyyy-MM-dd");
            var versionTicks = (record.UpdatedAt ?? record.CreatedAt).Ticks;
            return string.Join("|",
                $"RecordId={record.Id}",
                $"UserId={record.UserId}",
                $"Status={record.Status}",
                $"FullName={record.FullName}",
                $"DateOfBirth={profileDate}",
                $"Gender={record.Gender}",
                $"CccdEncrypted={record.Cccd}",
                $"PhoneEncrypted={record.PhoneNumber}",
                $"AddressEncrypted={record.Address}",
                $"BhxhCodeEncrypted={record.BhxhCode}",
                $"CompanyName={record.CompanyName}",
                $"Salary={record.Salary}",
                $"VersionTicks={versionTicks}");
        }
    }
}
