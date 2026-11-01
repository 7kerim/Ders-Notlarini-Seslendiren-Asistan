using Microsoft.Extensions.Logging;
using SesliAsistan.Business.Interfaces;

namespace SesliAsistan.Business.Services;

public class LoggerEmailSender : IEmailSender
{
    private readonly ILogger<LoggerEmailSender> _logger;

    public LoggerEmailSender(ILogger<LoggerEmailSender> logger) => _logger = logger;

    public Task SendPasswordResetCodeAsync(string toEmail, string code, CancellationToken ct = default)
    {
        _logger.LogInformation("Şifre sıfırlama kodu gönderildi -> E-posta: {Email}, Kod: {Code}", toEmail, code);
        return Task.CompletedTask;
    }
}
