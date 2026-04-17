using BHXH_Backend.Data;
using BHXH_Backend.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Controllers
{
    public class LookupSearchRequest
    {
        public string Keyword { get; set; } = string.Empty;
    }

    public class BhxhTimelineEntryDto
    {
        public required string Period { get; set; }
        public required string CompanyName { get; set; }
        public required string Salary { get; set; }
        public required string ContributionType { get; set; }
        public int Months { get; set; }
    }

    public class BhxhLookupResultDto
    {
        public required string FullName { get; set; }
        public required string DateOfBirth { get; set; }
        public required string Gender { get; set; }
        public required string Cccd { get; set; }
        public required string BhxhCode { get; set; }
        public string? CompanyName { get; set; }
        public decimal? Salary { get; set; }
        public required string Status { get; set; }
        public required string SubmittedAt { get; set; }
        public string? LastUpdatedAt { get; set; }
        public bool CccdVerified { get; set; }
        public bool OtpProvided { get; set; }
        public string? OtpNotice { get; set; }
        public int TotalMonths { get; set; }
        public string TotalDuration { get; set; } = string.Empty;
        public bool HasTimelineDetails { get; set; }
        public string Notes { get; set; } = string.Empty;
        public List<BhxhTimelineEntryDto> Timeline { get; set; } = new();
    }

    [Route("api/[controller]")]
    [ApiController]
    public class LookupController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public LookupController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("/api/search")]
        public async Task<IActionResult> Search([FromBody] LookupSearchRequest request)
        {
            var normalizedKeyword = HashHelper.NormalizeLookupKeyword(request.Keyword);
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                return BadRequest(new { message = "keyword là bắt buộc." });
            }

            var hashedKeyword = HashHelper.ToSha256(normalizedKeyword);
            await BackfillBhxhHashesAsync();

            var bhxhRecords = await _context.BhxhRecords
                .AsNoTracking()
                .Where(x => x.CccdHash == hashedKeyword || x.BhxhCodeHash == hashedKeyword)
                .Select(x => new
                {
                    x.Id,
                    x.UserId,
                    x.CompanyName,
                    x.Salary,
                    x.Status,
                    x.CreatedAt,
                    x.UpdatedAt
                })
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ToListAsync();

            var bhytRecord = await (
                from b in _context.BhytRecords.AsNoTracking()
                join x in _context.BhxhRecords.AsNoTracking() on b.UserId equals x.UserId
                where x.CccdHash == hashedKeyword || x.BhxhCodeHash == hashedKeyword
                orderby b.CreatedAt descending
                select new
                {
                    b.Id,
                    b.UserId,
                    b.CardNumber,
                    b.RegisteredHospital,
                    b.ValidFrom,
                    b.ValidTo,
                    b.BenefitRate,
                    b.Status
                })
                .FirstOrDefaultAsync();

            if (bhxhRecords.Count == 0 && bhytRecord == null)
            {
                return Ok(new { message = "Không tìm thấy thông tin" });
            }

            return Ok(new
            {
                bhxh = bhxhRecords,
                bhyt = bhytRecord
            });
        }

        [HttpGet("/api/bhxh/{user_id:int}")]
        public async Task<IActionResult> GetBhxhByUser(int user_id)
        {
            var records = await _context.BhxhRecords
                .AsNoTracking()
                .Where(x => x.UserId == user_id)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.UserId,
                    x.CompanyName,
                    x.Salary,
                    x.Status,
                    x.CreatedAt,
                    x.UpdatedAt
                })
                .ToListAsync();

            if (records.Count == 0)
            {
                return NotFound(new { message = "Không tìm thấy hồ sơ BHXH theo user_id." });
            }

            return Ok(records);
        }

        [HttpGet("/api/bhyt/{user_id:int}")]
        public async Task<IActionResult> GetBhytByUser(int user_id)
        {
            var record = await _context.BhytRecords
                .AsNoTracking()
                .Where(x => x.UserId == user_id)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.UserId,
                    x.CardNumber,
                    x.RegisteredHospital,
                    x.ValidFrom,
                    x.ValidTo,
                    x.BenefitRate,
                    x.Status
                })
                .FirstOrDefaultAsync();

            if (record == null)
            {
                return NotFound(new { message = "Không tìm thấy dữ liệu BHYT theo user_id." });
            }

            return Ok(record);
        }

        [HttpGet("bhxh")]
        public async Task<IActionResult> LegacyLookupBhxh([FromQuery] string query, [FromQuery] string? cccd, [FromQuery] string? otp)
        {
            var normalizedKeyword = HashHelper.NormalizeLookupKeyword(query);
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                return BadRequest(new { message = "Vui lòng nhập mã số BHXH hoặc CCCD." });
            }

            var aesKey = ConfigurationHelper.GetAesKey(_configuration);
            if (string.IsNullOrWhiteSpace(aesKey))
            {
                return StatusCode(500, new { message = "Hệ thống chưa cấu hình khóa AES." });
            }

            var hashedKeyword = HashHelper.ToSha256(normalizedKeyword);
            await BackfillBhxhHashesAsync();

            var matchedRecords = await _context.BhxhRecords
                .AsNoTracking()
                .Where(x => x.CccdHash == hashedKeyword || x.BhxhCodeHash == hashedKeyword)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ToListAsync();

            if (matchedRecords.Count == 0)
            {
                return Ok(new
                {
                    results = Array.Empty<object>(),
                    message = "Không tìm thấy hồ sơ BHXH với thông tin cung cấp."
                });
            }

            var requestedCccd = HashHelper.NormalizeLookupKeyword(cccd);
            var results = new List<BhxhLookupResultDto>();

            foreach (var record in matchedRecords)
            {
                var decryptedCccd = HashHelper.NormalizeLookupKeyword(SecurityHelper.Decrypt(record.Cccd, aesKey));
                var decryptedBhxh = HashHelper.NormalizeLookupKeyword(SecurityHelper.Decrypt(record.BhxhCode, aesKey));

                var cccdVerified = true;
                if (!string.IsNullOrWhiteSpace(requestedCccd))
                {
                    cccdVerified = string.Equals(requestedCccd, decryptedCccd, StringComparison.OrdinalIgnoreCase);
                    if (!cccdVerified)
                    {
                        return BadRequest(new { message = "CCCD không khớp với hồ sơ BHXH." });
                    }
                }

                var periodStart = record.CreatedAt.Date;
                var periodEnd = (record.UpdatedAt ?? DateTime.UtcNow).Date;
                var totalMonths = GetMonthCount(periodStart, periodEnd);
                var otpProvided = !string.IsNullOrWhiteSpace(otp);

                results.Add(new BhxhLookupResultDto
                {
                    FullName = record.FullName,
                    DateOfBirth = record.DateOfBirth.ToString("dd/MM/yyyy"),
                    Gender = record.Gender,
                    Cccd = decryptedCccd,
                    BhxhCode = decryptedBhxh,
                    CompanyName = string.IsNullOrWhiteSpace(record.CompanyName) ? "Chưa cập nhật" : record.CompanyName,
                    Salary = record.Salary,
                    Status = record.Status,
                    SubmittedAt = record.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    LastUpdatedAt = record.UpdatedAt?.ToString("dd/MM/yyyy HH:mm"),
                    CccdVerified = cccdVerified,
                    OtpProvided = otpProvided,
                    OtpNotice = otpProvided ? "OTP đã nhập, nhưng tính năng xác thực OTP tra cứu chưa bật." : null,
                    TotalMonths = totalMonths,
                    TotalDuration = totalMonths > 0 ? $"{totalMonths} tháng" : "Chưa xác định",
                    HasTimelineDetails = true,
                    Notes = "Hiển thị theo dữ liệu hồ sơ hiện có trong hệ thống.",
                    Timeline = new List<BhxhTimelineEntryDto>
                    {
                        new()
                        {
                            Period = $"{periodStart:MM/yyyy} - {periodEnd:MM/yyyy}",
                            CompanyName = string.IsNullOrWhiteSpace(record.CompanyName) ? "Chưa cập nhật" : record.CompanyName,
                            Salary = record.Salary.HasValue ? $"{record.Salary.Value:N0} đ" : "Chưa cập nhật",
                            ContributionType = "Bắt buộc",
                            Months = totalMonths
                        }
                    }
                });
            }

            return Ok(new
            {
                results,
                message = "Đã tìm thấy hồ sơ BHXH."
            });
        }

        [HttpGet("bhyt")]
        public async Task<IActionResult> LegacyLookupBhyt([FromQuery] string cardNumber, [FromQuery] string? fullName, [FromQuery] string? dateOfBirth)
        {
            var normalizedCardNumber = (cardNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedCardNumber))
            {
                return BadRequest(new { message = "Vui lòng nhập mã thẻ BHYT." });
            }

            var bhyt = await _context.BhytRecords
                .AsNoTracking()
                .Where(x => x.CardNumber == normalizedCardNumber)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    cardNumber = x.CardNumber,
                    fullName = string.IsNullOrWhiteSpace(fullName) ? "Chưa cung cấp" : fullName.Trim(),
                    dateOfBirth = string.IsNullOrWhiteSpace(dateOfBirth) ? "Chưa cung cấp" : dateOfBirth,
                    status = string.IsNullOrWhiteSpace(x.Status) ? "Chưa có dữ liệu BHYT" : x.Status,
                    validFrom = x.ValidFrom,
                    validTo = x.ValidTo,
                    registeredHospital = x.RegisteredHospital ?? "Chưa có dữ liệu",
                    benefitRate = x.BenefitRate ?? "Chưa có dữ liệu",
                    message = "Đã tra cứu dữ liệu BHYT.",
                    suggestedAction = (string?)null
                })
                .FirstOrDefaultAsync();

            if (bhyt == null)
            {
                return Ok(new
                {
                    supported = false,
                    cardNumber = normalizedCardNumber,
                    fullName = string.IsNullOrWhiteSpace(fullName) ? "Chưa cung cấp" : fullName.Trim(),
                    dateOfBirth = string.IsNullOrWhiteSpace(dateOfBirth) ? "Chưa cung cấp" : dateOfBirth,
                    status = "Chưa có dữ liệu BHYT",
                    validFrom = "-",
                    validTo = "-",
                    registeredHospital = "Chưa có dữ liệu",
                    benefitRate = "Chưa có dữ liệu",
                    message = "Tra cứu BHYT chưa có dữ liệu chi tiết.",
                    suggestedAction = "Vui lòng liên hệ cơ quan BHYT để kiểm tra chi tiết thẻ."
                });
            }

            return Ok(bhyt);
        }

        private async Task BackfillBhxhHashesAsync()
        {
            var aesKey = ConfigurationHelper.GetAesKey(_configuration);
            if (string.IsNullOrWhiteSpace(aesKey))
            {
                return;
            }

            var candidates = await _context.BhxhRecords
                .Where(x => string.IsNullOrWhiteSpace(x.CccdHash) || string.IsNullOrWhiteSpace(x.BhxhCodeHash))
                .ToListAsync();

            var hasChanges = false;
            foreach (var record in candidates)
            {
                if (string.IsNullOrWhiteSpace(record.CccdHash))
                {
                    var cccd = SecurityHelper.Decrypt(record.Cccd, aesKey);
                    record.CccdHash = HashHelper.ToSha256(cccd);
                    hasChanges = true;
                }

                if (string.IsNullOrWhiteSpace(record.BhxhCodeHash))
                {
                    var bhxhCode = SecurityHelper.Decrypt(record.BhxhCode, aesKey);
                    record.BhxhCodeHash = HashHelper.ToSha256(bhxhCode);
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
        }

        private static int GetMonthCount(DateTime start, DateTime end)
        {
            if (end < start)
            {
                return 0;
            }

            return (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
        }
    }
}
