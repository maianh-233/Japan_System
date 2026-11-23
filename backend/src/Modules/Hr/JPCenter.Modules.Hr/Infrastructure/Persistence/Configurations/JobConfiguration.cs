using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Configurations;

/// <summary>hr.jobs — database.md bảng 85.</summary>
internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> b)
    {
        b.ToTable("jobs");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.BranchId, x.Status });
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.SalaryRange).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Ignore(x => x.IsOpen);
    }
}
