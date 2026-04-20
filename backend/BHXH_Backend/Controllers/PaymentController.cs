using System.Security.Claims;
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
    public class PaymentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SystemLogService _logService;
        private readonly BlockchainService _blockchainService;
        private readonly VietQrService _qrService;

        public PaymentController(
            ApplicationDbContext context,
            SystemLogService logService,
            BlockchainService blockchainService,
            VietQrService qrService)
        {
            _context = context;
            _logService = logService;
            _blockchainService = blockchainService;
            _qrService = qrService;
        }

        [HttpPost("request")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> CreatePaymentRequest([FromBody] PaymentRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Dữ liệu yêu cầu thanh toán không hợp lệ." });
            }

            if (request.Amount < 100000)
            {
                return BadRequest(new { message = "Số tiền tối thiểu là 100.000 VND." });
            }

            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            var actor = User.FindFirst(ClaimTypes.Name)?.Value ?? "User";
            var actorIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
            var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            var qrAddInfo = description ?? string.Empty;

            var payment = new PaymentRecord
            {
                UserId = userId,
                BhxhCode = request.BhxhCode.Trim(),
                Amount = request.Amount,
                Currency = string.IsNullOrWhiteSpace(request.Currency) ? "VND" : request.Currency.Trim().ToUpperInvariant(),
                Description = description,
                PaymentCode = GeneratePaymentCode(),
                QrPayload = _qrService.GenerateQrPayload(request.Amount, qrAddInfo),
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentRecords.Add(payment);
            await _context.SaveChangesAsync();

            var blockchainMessage = BuildPaymentSnapshot(payment);
            var recordKey = GetPaymentRecordKey(payment.Id);
            var blockchainSynced = await _blockchainService.SubmitHashToBlockchainAsync(
                actor,
                "PAYMENT_REQUEST",
                blockchainMessage,
                actorIp,
                recordKey);

            payment.BlockchainSynced = blockchainSynced;
            await _context.SaveChangesAsync();

            await _logService.WriteLogAsync(
                actor,
                "PAYMENT_REQUEST",
                $"User {actor} dang yeu cau thanh toan BHXH. PaymentId={payment.Id}, Code={payment.PaymentCode}",
                actorIp);

            return Ok(new
            {
                message = "Yêu cầu thanh toán đã được tạo. Vui lòng chờ nhân viên xác nhận.",
                paymentId = payment.Id,
                paymentCode = payment.PaymentCode,
                amount = payment.Amount,
                currency = payment.Currency,
                description = payment.Description,
                qrPayload = payment.QrPayload,
                qrImageUrl = _qrService.GenerateQrImageUrl(payment.Amount, qrAddInfo),
                status = payment.Status,
                blockchainSynced
            });
        }

        private const int PaymentExpirationMinutes = 15;

        [HttpGet("payment-info")]
        [AllowAnonymous]
        public IActionResult GetPaymentInfo()
        {
            var info = _qrService.GetPaymentInfo();
            return Ok(info);
        }

        private async Task CancelExpiredPendingPaymentsAsync(CancellationToken cancellationToken = default)
        {
            var expirationTime = DateTime.UtcNow.AddMinutes(-PaymentExpirationMinutes);
            var expiredPayments = await _context.PaymentRecords
                .Where(p => p.Status == "Pending" && p.CreatedAt <= expirationTime)
                .ToListAsync(cancellationToken);

            if (!expiredPayments.Any())
            {
                return;
            }

            foreach (var payment in expiredPayments)
            {
                payment.Status = "Cancelled";
                payment.ReviewNote = "Tự động hủy sau 15 phút chưa thanh toán";
                payment.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        [HttpGet("my")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> GetMyPayments()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized();
            }

            await CancelExpiredPendingPaymentsAsync();

            var payments = await _context.PaymentRecords
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.BhxhCode,
                    p.Amount,
                    p.Currency,
                    p.PaymentCode,
                    p.Description,
                    p.Status,
                    p.ProcessedBy,
                    p.ReviewNote,
                    p.QrPayload,
                    p.CreatedAt,
                    p.UpdatedAt
                })
                .ToListAsync();

            return Ok(payments);
        }

        [HttpGet("requests")]
        [Authorize(Roles = "Employee,Staff")]
        public async Task<IActionResult> GetPaymentRequests([FromQuery] string? status)
        {
            await CancelExpiredPendingPaymentsAsync();

            var query = _context.PaymentRecords.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(p => p.Status == status.Trim());
            }

            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.UserId,
                    UserName = p.User != null ? p.User.FullName : null,
                    p.BhxhCode,
                    p.Amount,
                    p.Currency,
                    p.PaymentCode,
                    p.Description,
                    p.Status,
                    p.ProcessedBy,
                    p.ReviewNote,
                    p.CreatedAt,
                    p.UpdatedAt
                })
                .ToListAsync();

            return Ok(payments);
        }

        [HttpPut("requests/{id}/review")]
        [Authorize(Roles = "Employee,Staff")]
        public async Task<IActionResult> ReviewPaymentRequest(int id, [FromBody] ProcessRecordDto request)
        {
            var payment = await _context.PaymentRecords.FindAsync(id);
            if (payment == null)
            {
                return NotFound(new { message = "Yêu cầu thanh toán không tồn tại." });
            }

            if (payment.Status == "Confirmed")
            {
                return BadRequest(new { message = "Yêu cầu này đã được xác nhận." });
            }

            if (payment.Status == "Rejected")
            {
                return BadRequest(new { message = "Yêu cầu này đã bị từ chối." });
            }

            if (payment.Status == "Cancelled")
            {
                return BadRequest(new { message = "Yêu cầu đã bị hủy, không thể xử lý." });
            }

            if (request.Action != "Approved" && request.Action != "Rejected")
            {
                return BadRequest(new { message = "Hành động chỉ được phép là Approved hoặc Rejected." });
            }

            var actor = User.FindFirst(ClaimTypes.Name)?.Value ?? "Staff";
            var actorIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
            payment.Status = request.Action == "Approved" ? "Confirmed" : "Rejected";
            payment.ProcessedBy = actor;
            payment.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            payment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var blockchainMessage = BuildPaymentSnapshot(payment);
            var recordKey = GetPaymentRecordKey(payment.Id);
            var blockchainSynced = await _blockchainService.SubmitHashToBlockchainAsync(
                actor,
                "PAYMENT_REVIEW",
                blockchainMessage,
                actorIp,
                recordKey);

            payment.BlockchainSynced = blockchainSynced;
            await _context.SaveChangesAsync();

            await _logService.WriteLogAsync(
                actor,
                "PAYMENT_REVIEW",
                $"Staff {actor} da {(request.Action == "Approved" ? "xac nhan" : "tu choi")} payment ID={payment.Id}. Note={payment.ReviewNote}",
                actorIp);

            return Ok(new
            {
                message = request.Action == "Approved"
                    ? "Đã xác nhận thanh toán thành công."
                    : "Đã từ chối yêu cầu thanh toán.",
                status = payment.Status,
                blockchainSynced
            });
        }

        private static string GetPaymentRecordKey(int paymentId) => $"PAYMENT_REQUEST:{paymentId}";

        private static string GeneratePaymentCode()
        {
            return $"BHXHPAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()}";
        }

        private static string BuildPaymentSnapshot(PaymentRecord payment)
        {
            return string.Join("|",
                $"PaymentId={payment.Id}",
                $"UserId={payment.UserId}",
                $"BhxhCode={payment.BhxhCode}",
                $"Amount={payment.Amount}",
                $"Currency={payment.Currency}",
                $"PaymentCode={payment.PaymentCode}",
                $"Status={payment.Status}",
                $"ProcessedBy={payment.ProcessedBy}",
                $"ReviewNote={payment.ReviewNote}",
                $"UpdatedAt={(payment.UpdatedAt ?? payment.CreatedAt).Ticks}");
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("id");
            return int.TryParse(userIdStr, out userId);
        }
    }
}
