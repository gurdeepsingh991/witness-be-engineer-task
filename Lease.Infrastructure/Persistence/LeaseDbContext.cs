using Microsoft.EntityFrameworkCore;
using Lease.Infrastructure.Entities;

namespace Lease.Infrastructure.Persistence;

public class LeaseDbContext : DbContext
{
    public LeaseDbContext(DbContextOptions<LeaseDbContext> options)
        : base(options)
    {
    }

    public DbSet<LeaseResultEntity> LeaseResults => Set<LeaseResultEntity>();
    public DbSet<JobEntity> Jobs => Set<JobEntity>();
}