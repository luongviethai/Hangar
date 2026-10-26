namespace Hangar.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body);
}
