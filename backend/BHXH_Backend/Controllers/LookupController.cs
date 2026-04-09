using System.Text.RegularExpressions;
using BHXH_Backend.Data;
using BHXH_Backend.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Controllers
{
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
        private readonly IConfiguration _config;

        public LookupController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpGet("bhxh")]
        public async Task<IActionResult> LookupBhxh([FromQuery] string query, [FromQuery] string? cccd, [FromQuery] string? otp)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Vui lòng nhập mã số BHXH hoặc CCCD." });
            }

            var sanitizedQuery = NormalizeDigits(query.Trim());
            if (sanitizedQuery.Length < 8)
            {
                return BadRequest(new { message = "Mã số BHXH hoặc CCCD không hợp lệ." });
            }

            var aesKey = _config["AesSettings:Key"];
            if (string.IsNullOrWhiteSpace(aesKey))
            {
                return StatusCode(500, new { message = "Hệ thống chưa cấu hình AesSettings:Key." });
            }

            var records = await _context.BhxhRecords
                .AsNoTracking()
                .Select(r => new
                {
                    r.FullName,
                    r.DateOfBirth,
                    r.Gender,
                    r.Cccd,
                    r.BhxhCode,
                    r.CompanyName,
                    r.Salary,
                    r.Status,
                    r.CreatedAt,
                    r.UpdatedAt
                })
                .ToListAsync();

            var results = new List<BhxhLookupResultDto>();
            foreach (var record in records)
            {
                var decryptedBhxh = NormalizeDigits(SecurityHelper.Decrypt(record.BhxhCode, aesKey));
                var decryptedCccd = NormalizeDigits(SecurityHelper.Decrypt(record.Cccd, aesKey));

                if (string.Equals(decryptedBhxh, sanitizedQuery, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(decryptedCccd, sanitizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    var cccdVerified = false;
                    if (!string.IsNullOrWhiteSpace(cccd))
                    {
                        cccdVerified = string.Equals(NormalizeDigits(cccd), decryptedCccd, StringComparison.OrdinalIgnoreCase);
                        if (!cccdVerified)
                        {
                            return BadRequest(new { message = "CCCD không khớp với hồ sơ BHXH." });
                        }
                    }

                    var periodStart = record.CreatedAt.Date;
                    var periodEnd = (record.UpdatedAt ?? DateTime.UtcNow).Date;
                    var totalMonths = GetMonthCount(periodStart, periodEnd);
                    var timelineEntry = new BhxhTimelineEntryDto
                    {
                        Period = $"{periodStart:MM/yyyy} – {periodEnd:MM/yyyy}",
                        CompanyName = string.IsNullOrWhiteSpace(record.CompanyName) ? "Chưa cập nhật" : record.CompanyName,
                        Salary = record.Salary.HasValue ? record.Salary.Value.ToString("N0") + " đ" : "Chưa cập nhật",
                        ContributionType = "Bắt buộc",
                        Months = totalMonths
                    };

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
                        OtpProvided = !string.IsNullOrWhiteSpace(otp),
                        OtpNotice = !string.IsNullOrWhiteSpace(otp) ? "OTP đã nhập, nhưng chức năng xác thực OTP chưa được bật." : null,
                        TotalMonths = totalMonths,
                        TotalDuration = totalMonths > 0 ? $"{totalMonths} tháng" : "Chưa xác định",
                        HasTimelineDetails = true,
                        Notes = record.UpdatedAt.HasValue
                            ? "Dữ liệu BHXH hiện tại chỉ có một khoảng thời gian thay vì chi tiết đóng góp theo tháng."
                            : "Dữ liệu đóng BHXH chi tiết chưa đầy đủ. Hiển thị phạm vi từ ngày tạo hồ sơ đến hiện tại.",
                        Timeline = new List<BhxhTimelineEntryDto> { timelineEntry }
                    });
                }
            }

            return Ok(new
            {
                results,
                message = results.Any()
                    ? "Đã tìm thấy hồ sơ BHXH." 
                    : "Không tìm thấy hồ sơ BHXH với thông tin cung cấp."
            });
        }

        [HttpGet("bhyt")]
        public IActionResult LookupBhyt([FromQuery] string cardNumber, [FromQuery] string? fullName, [FromQuery] string? dateOfBirth)
        {
            if (string.IsNullOrWhiteSpace(cardNumber))
            {
                return BadRequest(new { message = "Vui lòng nhập mã thẻ BHYT." });
            }

            return Ok(new
            {
                supported = false,
                cardNumber = cardNumber.Trim(),
                fullName = string.IsNullOrWhiteSpace(fullName) ? "Chưa cung cấp" : fullName.Trim(),
                dateOfBirth = string.IsNullOrWhiteSpace(dateOfBirth) ? "Chưa cung cấp" : dateOfBirth,
                status = "Chưa có dữ liệu BHYT",
                validFrom = "-",
                validTo = "-",
                registeredHospital = "Chưa có dữ liệu",
                benefitRate = "Chưa có dữ liệu",
                message = "Tra cứu BHYT chưa được tích hợp đầy đủ backend. Dữ liệu hiện tại chỉ hiển thị định dạng thẻ.",
                suggestedAction = "Vui lòng liên hệ cơ quan BHYT để kiểm tra chi tiết thẻ."
            });
        }

        private static string NormalizeDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value.Where(char.IsDigit).ToArray());
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
