using System.Net.Mail;
using Hangar.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Hangar.Infrastructure.Email;

// MailHog ở local không cần TLS/auth — SmtpClient đơn giản là đủ cho mục tiêu học (bắt mail ở
// local, xem qua MailHog UI/API), không phải gửi mail thật ra Internet.
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;

    public SmtpEmailSender(IOptions<SmtpSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string body)
    {
        using var client = new SmtpClient(_settings.Host, _settings.Port);
        using var message = new MailMessage(_settings.FromAddress, toEmail, subject, body);

        await client.SendMailAsync(message);
    }
}
