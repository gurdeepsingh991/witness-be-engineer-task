using Lease.Infrastructure.Entities;
using Lease.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeaseApi.Repositories;

public class LeaseResultRepository
{
    private readonly LeaseDbContext _db;

    public LeaseResultRepository(LeaseDbContext db) => _db = db;

   public Task<LeaseResultEntity?> GetByTitleAsync(string titleNumber, CancellationToken ct) =>
    _db.LeaseResults.SingleOrDefaultAsync(x => x.TitleNumber == titleNumber, ct);

    public async Task SaveAsync(LeaseResultEntity entity, CancellationToken ct)
    {
        _db.LeaseResults.Add(entity);
        await _db.SaveChangesAsync(ct);
    }
}