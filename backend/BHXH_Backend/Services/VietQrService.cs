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
        /// Tạo mã QR thanh toán MB Bank theo chuẩn VietQR
        /// </summary>
        public string GenerateQrPayload(decimal amount, string description = "")
        {
            var bankCode = _configuration["Payment:BankCode"] ?? "MBB";
            var accountNumber = _configuration["Payment:AccountNumber"] ?? "";
            var accountName = _configuration["Payment:AccountName"] ?? "";

            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                throw new InvalidOperationException("Payment account number is not configured.");
            }

            // Format: {bankCode}-{accountNumber}-{amount}-0-{description}
            var amountFormatted = ((long)amount).ToString();
            var descFormatted = NormalizeDescription(description);
            var qrData = $"{bankCode.ToUpper()}-{accountNumber}-{amountFormatted}-0-{descFormatted}";

            return qrData;
        }

        /// <summary>
        /// Tạo URL hình ảnh QR từ VietQR API
        /// </summary>
        public string GenerateQrImageUrl(string qrPayload)
        {
            return $"https://api.vietqr.io/image/{Uri.EscapeDataString(qrPayload)}.png";
        }

        /// <summary>
        /// Lấy thông tin thanh toán từ cấu hình
        /// </summary>
        public Dictionary<string, string> GetPaymentInfo()
        {
            return new Dictionary<string, string>
            {
                { "BankCode", _configuration["Payment:BankCode"] ?? "MBB" },
                { "AccountNumber", _configuration["Payment:AccountNumber"] ?? "" },
                { "AccountName", _configuration["Payment:AccountName"] ?? "" },
                { "Branch", _configuration["Payment:Branch"] ?? "" }
            };
        }

        private static string NormalizeDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return "BHXH";
            }

            // Loại bỏ ký tự đặc biệt, giữ lại chữ số và chữ cái
            var normalized = System.Text.RegularExpressions.Regex.Replace(description, @"[^a-zA-Z0-9\s]", "");
            // Cắt ngắn nếu quá dài
            return normalized.Length > 30 ? normalized.Substring(0, 30) : normalized;
        }
    }
}
