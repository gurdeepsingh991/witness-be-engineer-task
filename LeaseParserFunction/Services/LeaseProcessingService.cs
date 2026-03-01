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
                    System.Text.RegularExpressions.Regex.IsMatch(t, $@"\b{System.Text.RegularExpressions.Regex.Escape(titleNumber)}\b"))).ToList();

            if (filtered.Count == 0)
            {
                job.Status = JobStatus.NotFound;
                job.LastError = null;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();
                return;
            }

            var parsed = _parser.Parse(filtered).ToList();

            if (parsed.Count == 0 || !parsed.Any(p => 
                !string.IsNullOrWhiteSpace(p.PropertyDescription) ||
                !string.IsNullOrWhiteSpace(p.RegistrationDateAndPlanRef) ||
                !string.IsNullOrWhiteSpace(p.DateOfLeaseAndTerm) ||
                !string.IsNullOrWhiteSpace(p.LesseesTitle)))
            {
                job.Status = JobStatus.NotFound;
                job.LastError = null;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();
                return;
            }

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