namespace Lease.Infrastructure.Entities;

public class JobEntity
{
    public Guid Id { get; set; } 
    public string TitleNumber { get; set; } = default!;
    public string Status { get; set; } = default!; 
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}