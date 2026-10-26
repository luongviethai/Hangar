namespace Hangar.Infrastructure.Email;

public class SmtpSettings
{
    public required string Host { get; set; }
    public int Port { get; set; } = 1025;
    public string FromAddress { get; set; } = "no-reply@hangar.local";
}
