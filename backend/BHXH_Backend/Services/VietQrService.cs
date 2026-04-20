using System.Globalization;
using System.Text.RegularExpressions;

namespace BHXH_Backend.Services
{
    public class VietQrService
    {
        private readonly IConfiguration _configuration;

        public VietQrService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Tạo mã QR thanh toán MB Bank theo chuẩn payload nội bộ.
        /// </summary>
        public string GenerateQrPayload(decimal amount, string description = "")
        {
            var bankCode = _configuration["Payment:BankCode"] ?? "MBB";
            var accountNumber = _configuration["Payment:AccountNumber"] ?? "";

            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                throw new InvalidOperationException("Payment account number is not configured.");
            }

            var amountFormatted = decimal.Truncate(amount).ToString(CultureInfo.InvariantCulture);
            var descFormatted = NormalizeDescription(description);
            return $"{bankCode.ToUpperInvariant()}-{accountNumber}-{amountFormatted}-0-{descFormatted}";
        }

        /// <summary>
        /// Tạo URL ảnh QR cho frontend theo chuẩn VietQR image.
        /// </summary>
        public string GenerateQrImageUrl(decimal amount, string addInfo = "")
        {
            var bankCode = NormalizeBankCodeForImage(_configuration["Payment:BankCode"] ?? "MB");
            var accountNumber = _configuration["Payment:AccountNumber"] ?? "";
            var accountName = _configuration["Payment:AccountName"] ?? "";

            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                throw new InvalidOperationException("Payment account number is not configured.");
            }

            var queryParts = new List<string>
            {
                $"accountName={Uri.EscapeDataString(accountName)}"
            };

            if (amount > 0)
            {
                var normalizedAmount = decimal.Truncate(amount).ToString(CultureInfo.InvariantCulture);
                queryParts.Add($"amount={Uri.EscapeDataString(normalizedAmount)}");
            }

            if (!string.IsNullOrWhiteSpace(addInfo))
            {
                queryParts.Add($"addInfo={Uri.EscapeDataString(addInfo.Trim())}");
            }

            return $"https://img.vietqr.io/image/{bankCode}-{accountNumber}-compact.png?{string.Join("&", queryParts)}";
        }

        /// <summary>
        /// Tạo URL ảnh QR từ payload (giữ tương thích ngược).
        /// </summary>
        public string GenerateQrImageUrl(string qrPayload)
        {
            return $"https://api.vietqr.io/image/{Uri.EscapeDataString(qrPayload)}.png";
        }

        /// <summary>
        /// Lấy thông tin thanh toán từ cấu hình.
        /// </summary>
        public Dictionary<string, string> GetPaymentInfo()
        {
            var bankCode = _configuration["Payment:BankCode"] ?? "MBB";

            return new Dictionary<string, string>
            {
                { "BankCode", bankCode },
                { "BankCodeForImage", NormalizeBankCodeForImage(bankCode) },
                { "AccountNumber", _configuration["Payment:AccountNumber"] ?? "" },
                { "AccountName", _configuration["Payment:AccountName"] ?? "" },
                { "Branch", _configuration["Payment:Branch"] ?? "" }
            };
        }

        private static string NormalizeBankCodeForImage(string bankCode)
        {
            var normalized = (bankCode ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                "MBB" => "MB",
                "MBBANK" => "MB",
                _ => string.IsNullOrWhiteSpace(normalized) ? "MB" : normalized
            };
        }

        private static string NormalizeDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return "BHXH";
            }

            var normalized = Regex.Replace(description, @"[^a-zA-Z0-9\s]", "");
            return normalized.Length > 30 ? normalized[..30] : normalized;
        }
    }
}
