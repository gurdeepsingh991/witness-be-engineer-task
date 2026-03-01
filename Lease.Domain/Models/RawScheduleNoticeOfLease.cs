namespace Lease.Domain.Models;

public class RawScheduleNoticeOfLease
{
    public string EntryNumber { get; set; } = default!;
    public string EntryDate { get; set; } = default!;
    public string EntryType { get; set; } = default!;
    public List<string> EntryText { get; set; } = new();
}