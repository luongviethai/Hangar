namespace Hangar.Api.Uploads;

public class FileTooLargeException : Exception
{
    public FileTooLargeException(long maxSizeBytes) : base($"File vượt quá giới hạn {maxSizeBytes} bytes.") { }
}
