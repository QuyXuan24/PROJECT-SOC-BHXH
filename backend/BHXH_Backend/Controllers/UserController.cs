using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BHXH_Backend.Data;
using BHXH_Backend.Helpers;
using BHXH_Backend.Models;
using BHXH_Backend.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BHXH_Backend.Controllers
{
    // DTO: Khuon mau du lieu User gui len.
    public class UserProfileDto
    {
        public required string FullName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public required string Gender { get; set; }

        public required string Cccd { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Address { get; set; }
        public required string BhxhCode { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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

            var actor = User.Identity?.Name ?? "Unknown";
            var actorIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

            var record = await _context.BhxhRecords.FirstOrDefaultAsync(r => r.UserId == userId);
            var aesKey = _config["AesSettings:Key"];
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
                message = "Nop ho so thanh cong! Vui long cho Staff xet duyet.",
                blockchainSynced
            });
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

            var aesKey = _config["AesSettings:Key"];
            if (string.IsNullOrWhiteSpace(aesKey))
            {
                return StatusCode(500, new { message = "He thong chua cau hinh AesSettings:Key." });
            }

            return Ok(new
            {
                Status = record.Status,
                Note = record.Status == "Pending" ? "Dang cho duyet" : "Da duoc duyet",
                FullName = record.FullName,
                DateOfBirth = record.DateOfBirth.ToString("dd/MM/yyyy"),
                Gender = record.Gender,
                Cccd = SecurityHelper.Decrypt(record.Cccd, aesKey),
                PhoneNumber = SecurityHelper.Decrypt(record.PhoneNumber, aesKey),
                Address = SecurityHelper.Decrypt(record.Address, aesKey),
                BhxhCode = SecurityHelper.Decrypt(record.BhxhCode, aesKey)
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
            // Snapshot hash dựa trên dữ liệu đã mã hóa AES và metadata quan trọng.
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
                $"VersionTicks={versionTicks}");
        }
    }
}
