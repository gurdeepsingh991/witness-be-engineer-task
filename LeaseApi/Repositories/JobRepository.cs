using Lease.Infrastructure.Entities;
using Lease.Domain.Enums;
using Lease.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeaseApi.Repositories;

public sealed class JobRepository
{
    private readonly LeaseDbContext _db;

    public JobRepository(LeaseDbContext db) => _db = db;

    public Task<JobEntity?> GetByTitleAsync(string titleNumber, CancellationToken ct) =>
        _db.Jobs.SingleOrDefaultAsync(x => x.TitleNumber == titleNumber, ct);

    public async Task<JobEntity> CreateIfMissingAsync(string titleNumber, CancellationToken ct)
    {
        // Fast path: most requests will hit an existing job/result.
        var existing = await GetByTitleAsync(titleNumber, ct);
        if (existing != null) return existing;

        var job = new JobEntity
        {
            Id = Guid.NewGuid(),
            TitleNumber = titleNumber,
            Status = JobStatus.Pending,
            AttemptCount = 0,
            LastError = null,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Jobs.Add(job);

        try
        {
            await _db.SaveChangesAsync(ct);
            return job;
        }
        catch (DbUpdateException)
        {
            // Another request likely created the job concurrently (unique TitleNumber).
            var winner = await GetByTitleAsync(titleNumber, ct);
            if (winner != null) return winner;

            throw;
        }
    }

    public async Task UpdateAsync(JobEntity job, CancellationToken ct)
    {
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}