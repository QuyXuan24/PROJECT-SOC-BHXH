using System.Net;
using System.Net.Mail;

namespace BHXH_Backend.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendOtpAsync(string toEmail, string otp, CancellationToken cancellationToken = default)
        {
            var emailUser = Environment.GetEnvironmentVariable("EMAIL_USER")
                ?? _configuration["Email:User"];
            var emailPass = Environment.GetEnvironmentVariable("EMAIL_PASS")
                ?? _configuration["Email:Pass"];
            var appName = Environment.GetEnvironmentVariable("APP_NAME")
                ?? _configuration["Email:AppName"]
                ?? "BHXH_SYSTEM";
            var host = Environment.GetEnvironmentVariable("EMAIL_HOST")
                ?? _configuration["Email:Host"]
                ?? "smtp.gmail.com";
            var portValue = Environment.GetEnvironmentVariable("EMAIL_PORT")
                ?? _configuration["Email:Port"];
            var port = int.TryParse(portValue, out var parsedPort) ? parsedPort : 587;

            if (string.IsNullOrWhiteSpace(emailUser) || string.IsNullOrWhiteSpace(emailPass))
            {
                throw new InvalidOperationException("Missing EMAIL_USER/EMAIL_PASS for SMTP.");
            }

            using var message = new MailMessage
            {
                From = new MailAddress(emailUser, appName),
                Subject = "[BHXH] Mã OTP xác thực",
                Body = $"Mã OTP: {otp}\nHiệu lực 5 phút\nKhông chia sẻ mã này",
                IsBodyHtml = false
            };

            message.To.Add(new MailAddress(toEmail));

            // Gmail SMTP with port 587 uses STARTTLS.
            using var smtpClient = new SmtpClient(host, port)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(emailUser, emailPass)
            };

            cancellationToken.ThrowIfCancellationRequested();
            await smtpClient.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("OTP email sent to {Email}", toEmail);
        }
    }
}
