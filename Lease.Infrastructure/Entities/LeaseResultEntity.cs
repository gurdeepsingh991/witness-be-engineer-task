namespace Lease.Infrastructure.Entities;

public class LeaseResultEntity{
    public Guid Id { get; set; } 
    public string TitleNumber { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}