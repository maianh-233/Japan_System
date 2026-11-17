using JPCenter.Application.Common.Security;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.IntegrationTests.BuildingBlocks;

/// <summary>Kiểm tra quy ước DbContext gốc (CLAUDE.md mục 7) trên PostgreSQL thật.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ModuleDbContextConventionTests(PostgresFixture pg) : IAsyncLifetime
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 3, 0, 0, TimeSpan.Zero);

    private SampleDbContext NewContext() => new(new DbContextOptionsBuilder<SampleDbContext>()
        .UseNpgsql(pg.ConnectionString)
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(new AuditFieldsInterceptor(new FixedClock(), new FixedUser()))
        .Options);

    public async ValueTask InitializeAsync()
    {
        await using var db = NewContext();
        await db.Database.EnsureCreatedAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Luu_va_doc_lai_Id_enum_tien_danh_sach_chi_nhanh()
    {
        var branch = BranchId.New();
        var sample = new Sample("Lớp N3", SampleStatus.TestScheduled, Money.FromTrusted(4_500_000), [branch]);
        await using (var db = NewContext())
        {
            db.Samples.Add(sample);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = NewContext();
        var loaded = await read.Samples.SingleAsync(x => x.Id == sample.Id, TestContext.Current.CancellationToken);
        loaded.Status.ShouldBe(SampleStatus.TestScheduled);
        loaded.Fee.Amount.ShouldBe(4_500_000m);
        loaded.BranchIds.ShouldBe([branch]);

        // Enum lưu chuỗi snake_case, cột chung được điền
        var raw = await read.Database.SqlQuery<string>(
            $"select status as \"Value\" from sample.samples where id = {sample.Id.Value}").SingleAsync(TestContext.Current.CancellationToken);
        raw.ShouldBe("test_scheduled");
        read.Entry(loaded).Property<DateTimeOffset>(ShadowColumns.CreatedAt).CurrentValue.ShouldBe(Now);
        read.Entry(loaded).Property<Guid?>(ShadowColumns.CreatedBy).CurrentValue.ShouldBe(Actor);
    }

    [Fact]
    public async Task Xoa_la_xoa_mem_va_bi_an_khoi_truy_van()
    {
        var sample = new Sample("Xóa thử", SampleStatus.New, Money.Zero, []);
        await using (var db = NewContext())
        {
            db.Samples.Add(sample);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            db.Samples.Remove(sample);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = NewContext();
        (await read.Samples.AnyAsync(x => x.Id == sample.Id, TestContext.Current.CancellationToken)).ShouldBeFalse();
        var trashed = await read.Samples.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter])
            .SingleAsync(x => x.Id == sample.Id, TestContext.Current.CancellationToken);
        read.Entry(trashed).Property<bool>(ShadowColumns.IsDeleted).CurrentValue.ShouldBeTrue();
        read.Entry(trashed).Property<Guid?>(ShadowColumns.DeletedBy).CurrentValue.ShouldBe(Actor);
    }

    [Fact]
    public async Task Hai_nguoi_sua_cung_luc_thi_nguoi_sau_bi_tu_choi()
    {
        var sample = new Sample("Gốc", SampleStatus.New, Money.Zero, []);
        await using (var db = NewContext())
        {
            db.Samples.Add(sample);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var a = NewContext();
        await using var b = NewContext();
        var fromA = await a.Samples.SingleAsync(x => x.Id == sample.Id, TestContext.Current.CancellationToken);
        var fromB = await b.Samples.SingleAsync(x => x.Id == sample.Id, TestContext.Current.CancellationToken);

        fromA.Rename("A sửa");
        await a.SaveChangesAsync(TestContext.Current.CancellationToken);

        fromB.Rename("B sửa");
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
        public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);
    }

    private sealed class FixedUser : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? AccountId => Actor;
        public Guid? SessionId => null;
        public UserAccess? Access => null;
    }
}
