using System.Text.Json;
using Lease.Domain.Enums;
using Lease.Domain.Parsers;
using Lease.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeaseParserFunction.Services;

public class LeaseProcessingService
{
    private readonly HmlrClient _client;
    private readonly ILeaseParser _parser;
    private readonly LeaseDbContext _db;

    public LeaseProcessingService(
        HmlrClient client,
        ILeaseParser parser,
        LeaseDbContext db)
    {
        _client = client;
        _parser = parser;
        _db = db;
    }

    public async Task ProcessAsync(string titleNumber)
    {
        var job = await _db.Jobs
            .SingleOrDefaultAsync(x => x.TitleNumber == titleNumber);

        if (job is null)
            return;

        try
        {
            var raw = await _client.GetSchedulesAsync();

            var filtered = raw.Where(x =>
                x.EntryText != null &&
                x.EntryText.Any(t =>
                    !string.IsNullOrWhiteSpace(t) &&
                    t.Contains(titleNumber)));

            var parsed = _parser.Parse(filtered);

            var json = JsonSerializer.Serialize(parsed);

            _db.LeaseResults.Add(new()
            {
                Id = Guid.NewGuid(),
                TitleNumber = titleNumber,
                PayloadJson = json,
                CreatedAt = DateTimeOffset.UtcNow
            });

            job.Status = JobStatus.Completed;
            job.LastError = null;
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.LastError = ex.Message;
            throw;
        }

        job.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
    }
}