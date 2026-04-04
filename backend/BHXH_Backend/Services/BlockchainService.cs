using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BHXH_Backend.Services;

public class BlockchainService
{
    private readonly string _bridgeUrl;
    private readonly string _verifyUrl;
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly ILogger<BlockchainService> _logger;

    public BlockchainService(IConfiguration configuration, ILogger<BlockchainService> logger)
    {
        _bridgeUrl = configuration["BlockchainSettings:BridgeUrl"]
            ?? throw new Exception("Chua cau hinh BridgeUrl trong appsettings.json");
        _verifyUrl = configuration["BlockchainSettings:VerifyUrl"]
            ?? DeriveVerifyUrl(_bridgeUrl);
        _logger = logger;
    }

    public async Task<bool> SubmitHashToBlockchainAsync(
        string username,
        string logType,
        string message,
        string? ipAddress = null,
        string? recordKey = null)
    {
        try
        {
            var safeUsername = string.IsNullOrWhiteSpace(username) ? "System" : username.Trim();
            var safeLogType = string.IsNullOrWhiteSpace(logType) ? "SYSTEM_EVENT" : logType.Trim();
            var safeMessage = string.IsNullOrWhiteSpace(message) ? "No message" : message.Trim();
            var safeIpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "Unknown IP" : ipAddress.Trim();
            var safeRecordKey = string.IsNullOrWhiteSpace(recordKey)
                ? $"{safeLogType}:{safeUsername}"
                : recordKey.Trim();

            // Hash on dinh theo khoa ban ghi + snapshot du lieu de verify duoc theo phien ban hien tai.
            var hashInput = BuildStableHashInput(safeRecordKey, safeMessage);
            var dataHash = ComputeSha256(hashInput);

            var payload = new
            {
                username = safeUsername,
                logType = safeLogType,
                message = safeMessage,
                hash = dataHash,
                ipAddress = safeIpAddress,
                recordKey = safeRecordKey
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Submitting hash to blockchain bridge at {BridgeUrl}", _bridgeUrl);
            var response = await _httpClient.PostAsync(_bridgeUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Blockchain bridge rejected request. StatusCode={StatusCode}",
                    response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit hash to blockchain bridge.");
            return false;
        }
    }

    public async Task<bool> SubmitHashToBlockchainAsync(string dataId, string inputData)
    {
        return await SubmitHashToBlockchainAsync(
            dataId,
            "DATA_HASH",
            inputData,
            "Unknown IP",
            dataId);
    }

    public async Task<(bool verified, string? chainHash, string? requestHash)> VerifyHashOnBlockchainAsync(
        string recordKey,
        string _username,
        string _logType,
        string message,
        string? _ipAddress = null)
    {
        try
        {
            var safeMessage = string.IsNullOrWhiteSpace(message) ? "No message" : message.Trim();
            var safeRecordKey = string.IsNullOrWhiteSpace(recordKey) ? "UNKNOWN_KEY" : recordKey.Trim();

            var hashInput = BuildStableHashInput(safeRecordKey, safeMessage);
            var requestHash = ComputeSha256(hashInput);

            var payload = new
            {
                recordKey = safeRecordKey,
                hash = requestHash
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Verifying hash on blockchain bridge at {VerifyUrl}", _verifyUrl);
            var response = await _httpClient.PostAsync(_verifyUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Blockchain verify request failed. StatusCode={StatusCode}",
                    response.StatusCode);
                return (false, null, requestHash);
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var verifyResult = JsonSerializer.Deserialize<BridgeVerifyResponse>(responseText);
            if (verifyResult == null)
            {
                return (false, null, requestHash);
            }

            return (verifyResult.success && verifyResult.verified, verifyResult.chainHash, requestHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify hash on blockchain bridge.");
            return (false, null, null);
        }
    }

    private static string ComputeSha256(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    private static string BuildStableHashInput(string recordKey, string message)
    {
        return $"{recordKey}|{message}";
    }

    private static string DeriveVerifyUrl(string bridgeUrl)
    {
        if (string.IsNullOrWhiteSpace(bridgeUrl))
        {
            return "http://blockchain_bridge:3001/verify";
        }

        if (bridgeUrl.EndsWith("/submit", StringComparison.OrdinalIgnoreCase))
        {
            return bridgeUrl[..^"submit".Length] + "verify";
        }

        return bridgeUrl.TrimEnd('/') + "/verify";
    }

    private sealed class BridgeVerifyResponse
    {
        public bool success { get; set; }
        public bool verified { get; set; }
        public string? chainHash { get; set; }
    }
}
