namespace Hangar.Application.Builds;

public record CreateBuildResult(int Id, string FileName, long SizeBytes, DateTime CreatedAtUtc);