using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace BHXH_Backend.Services;
public class BlockchainService 
{
    private readonly string _bridgeUrl;
    private readonly HttpClient _httpClient = new HttpClient();

    //Hàm khởi tạo để lấy cấu hình từ appsettings.json
    public BlockchainService(IConfiguration configuration)
    {
        _bridgeUrl = configuration["BlockchainSettings:BridgeUrl"] 
                     ?? throw new Exception("Chưa cấu hình BridgeUrl trong appsettings.json");
    }

    public async Task<bool> SubmitHashToBlockchainAsync(string dataId, string inputData)
    {
        try {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(inputData));
            string dataHash = BitConverter.ToString(bytes).Replace("-", "").ToLower();

            //GỬI SANG UBUNTU QUA IP TRONG CONFIG
            var payload = new { id = dataId, hash = dataHash };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            Console.WriteLine($"Đang gửi Hash {dataHash} tới: {_bridgeUrl}");
            var response = await _httpClient.PostAsync(_bridgeUrl, content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) {
            Console.WriteLine($" Lỗi: {ex.Message}");
            return false;
        }
    }
}