using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.IntegrationTests.BuildingBlocks;

// Mô hình nhỏ chỉ để kiểm tra quy ước của ModuleDbContextBase trên PostgreSQL thật.

public readonly record struct SampleId(Guid Value)
{
    public static SampleId New() => new(IdGenerator.NewId());
}

public enum SampleStatus
{
    New,
    TestScheduled,
}

public sealed class Sample : AggregateRoot<SampleId>, IAuditable, ISoftDeletable
{
    private Sample() { }

    public Sample(string name, SampleStatus status, Money fee, List<BranchId> branchIds) : base(SampleId.New()) =>
        (Name, Status, Fee, BranchIds) = (name, status, fee, branchIds);

    public string Name { get; private set; } = null!;
    public SampleStatus Status { get; private set; }
    public Money Fee { get; private set; }
    public List<BranchId> BranchIds { get; private set; } = [];

    public void Rename(string name) => Name = name;
}

public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : ModuleDbContextBase(options)
{
    protected override string Schema => "sample";

    public DbSet<Sample> Samples => Set<Sample>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sample>(b =>
        {
            b.ToTable("samples");
            b.HasKey(x => x.Id);
        });
        base.OnModelCreating(modelBuilder);
    }
}
