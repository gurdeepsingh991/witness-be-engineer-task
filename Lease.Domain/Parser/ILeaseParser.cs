using Lease.Domain.Models;

namespace Lease.Domain.Parsers;

public interface ILeaseParser
{
    IEnumerable<ParsedScheduleNoticeOfLease> Parse(
        IEnumerable<RawScheduleNoticeOfLease> rawItems);
}