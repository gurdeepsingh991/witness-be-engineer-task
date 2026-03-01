namespace Lease.Domain.Models;

public class ParsedScheduleNoticeOfLease
{
    public int EntryNumber { get; set; }
    public DateTime? EntryDate { get; set; }
    public string RegistrationDateAndPlanRef { get; set; } = default!;
    public string PropertyDescription { get; set; } = default!;
    public string DateOfLeaseAndTerm { get; set; } = default!;
    public string LesseesTitle { get; set; } = default!;
    public List<string>? Notes { get; set; }
}