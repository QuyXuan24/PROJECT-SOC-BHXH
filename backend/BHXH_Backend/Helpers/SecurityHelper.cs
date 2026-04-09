using System.Security.Cryptography;
using System.Text;

namespace BHXH_Backend.Helpers
{
    public static class SecurityHelper
    {
        // 1. HÀM MÃ HÓA (Biến chữ thường -> chữ mã hóa)
        // Dùng khi User nộp hồ sơ vào Database
        public static string Encrypt(string plainText, string key)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            if (keyBytes.Length != 32)
            {
                throw new ArgumentException("AES key must be 32 bytes (AES-256).");
            }

            var iv = RandomNumberGenerator.GetBytes(16);

            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (var sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }
                    }

                    // Luu theo dinh dang iv:cipher de decrypt duoc o cac lan sau.
                    return $"{Convert.ToBase64String(iv)}:{Convert.ToBase64String(ms.ToArray())}";
                }
            }
        }

        // 2. HÀM GIẢI MÃ (Biến kí tự mã hóa -> chữ thường)
        // Dùng khi User xem lại hồ sơ hoặc Staff duyệt
        public static string Decrypt(string cipherText, string key)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try 
            {
                byte[] keyBytes = Encoding.UTF8.GetBytes(key);
                if (keyBytes.Length != 32)
                {
                    throw new ArgumentException("AES key must be 32 bytes (AES-256).");
                }

                byte[] iv;
                byte[] buffer;
                var parts = cipherText.Split(':', 2);

                // Backward-compatible: du lieu cu khong co IV se dung zero IV.
                if (parts.Length == 2)
                {
                    iv = Convert.FromBase64String(parts[0]);
                    buffer = Convert.FromBase64String(parts[1]);
                }
                else
                {
                    iv = new byte[16];
                    buffer = Convert.FromBase64String(cipherText);
                }

                using (var aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (var ms = new MemoryStream(buffer))
                    {
                        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        {
                            using (var sr = new StreamReader(cs))
                            {
                                return sr.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Nếu key sai hoặc dữ liệu lỗi thì trả về rỗng 
                return ""; 
            }
        }
    }
}
