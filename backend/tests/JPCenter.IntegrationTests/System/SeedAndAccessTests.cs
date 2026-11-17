using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Idempotency;
using JPCenter.Application.Common.Security;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.System;

/// <summary>Dữ liệu mẫu + nạp quyền hiệu lực (PQ-07) + nhật ký / Idempotency lưu PostgreSQL — trên DB thật.</summary>
[Collection(SeededCollection.Name)]
public sealed class SeedAndAccessTests(SeededApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<UserAccess> AccessOf(string email)
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
        var staff = await db.UserAccounts.AsNoTracking().Where(x => x.Email == email).Select(x => (Guid?)x.Id.Value).FirstOrDefaultAsync(Ct);
        var portal = staff is null
            ? await db.PortalAccounts.AsNoTracking().Where(x => x.Email == email).Select(x => x.Id.Value).FirstAsync(Ct)
            : Guid.Empty;
        var provider = scope.ServiceProvider.GetRequiredService<IUserAccessProvider>();
        return (await provider.LoadAsync(staff ?? portal, staff is null, Ct))!;
    }

    [Fact]
    public async Task Du_lieu_mau_duoc_nap_day_du()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
        (await db.Branches.CountAsync(Ct)).ShouldBe(4);
        (await db.Roles.CountAsync(Ct)).ShouldBe(10);
        (await db.UserAccounts.CountAsync(Ct)).ShouldBeGreaterThan(20);
        (await db.PortalAccounts.CountAsync(Ct)).ShouldBeGreaterThan(20);
        (await db.AuditLogs.CountAsync(Ct)).ShouldBeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public async Task Super_admin_toan_quyen()
    {
        var a = await AccessOf("superadmin@jpcenter.vn");
        a.IsSuperAdmin.ShouldBeTrue();
        a.Portal.ShouldBe(Portal.Staff);
        a.DisplayName.ShouldBe("Đỗ Quang Huy");
        a.AllBranches.ShouldBeTrue();
    }

    [Fact]
    public async Task Giao_vu_2_chi_nhanh_va_quyen_le_xem_cong_no()
    {
        var a = await AccessOf("giaovu@jpcenter.vn");
        a.IsSuperAdmin.ShouldBeFalse();
        a.BranchIds.Count.ShouldBe(2);

        var students = a.Permissions["students.profiles.view"];
        students.Scope.ShouldBe(PermissionScope.Branch);
        students.BranchIds.Count.ShouldBe(2);

        // Quyền lẻ còn hạn (OV-1) — không có trong role Giáo vụ
        a.Permissions["finance.debts.view"].Scope.ShouldBe(PermissionScope.Branch);
        a.Permissions.ShouldNotContainKey("finance.invoices.delete");
    }

    [Fact]
    public async Task Quyen_le_het_han_khong_con_tac_dung()
    {
        var a = await AccessOf("hong.hong@jpcenter.vn");
        a.Permissions.ShouldNotContainKey("finance.reconciliation.view");
    }

    [Fact]
    public async Task Role_gan_theo_chi_nhanh_thi_quyen_toan_he_thong_thu_ve_chi_nhanh()
    {
        // Lễ tân Q1 kiêm Sale Q1: crm.campaigns.view của Sale là "all" nhưng role gán ở Q1 → "branch"
        var a = await AccessOf("linh.linh@jpcenter.vn");
        a.Permissions["crm.campaigns.view"].Scope.ShouldBe(PermissionScope.Branch);
        a.RoleName.ShouldContain("Sale");
    }

    [Fact]
    public async Task Giao_vien_vao_cong_giao_vien()
    {
        var a = await AccessOf("giaovien@jpcenter.vn");
        a.Portal.ShouldBe(Portal.Teacher);
        a.DisplayName.ShouldBe("Nguyễn Thu Hà");
    }

    [Fact]
    public async Task Hoc_vien_va_phu_huynh_vao_cong_rieng()
    {
        var student = await AccessOf("hocvien@jpcenter.vn");
        student.Portal.ShouldBe(Portal.Student);
        student.DisplayName.ShouldBe("Nguyễn Văn An");
        student.OwnerId.ShouldNotBeNull();
        student.Permissions.ShouldBeEmpty();

        (await AccessOf("phuhuynh@jpcenter.vn")).Portal.ShouldBe(Portal.Parent);
    }

    [Fact]
    public async Task Nhat_ky_ghi_vao_bang_audit_logs()
    {
        await using (var scope = api.Scope())
        {
            scope.ServiceProvider.GetRequiredService<IAuditTrail>()
                .Record(AuditCategory.Setting, AuditSeverity.Info, "Thử ghi nhật ký", "test-audit-target");
        }

        for (var i = 0; i < 60; i++)
        {
            await using var scope = api.Scope();
            var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
            if (await db.AuditLogs.AnyAsync(x => x.Target == "test-audit-target", Ct)) return;
            await Task.Delay(50, Ct);
        }
        throw new TimeoutException("Không thấy dòng nhật ký trong audit_logs");
    }

    [Fact]
    public async Task Idempotency_key_luu_PostgreSQL()
    {
        await using var scope = api.Scope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var key = Guid.NewGuid().ToString("N");

        (await store.TryBeginAsync("user:x", key, "h1", Ct)).State.ShouldBe(IdempotencyState.Started);
        (await store.TryBeginAsync("user:x", key, "h1", Ct)).State.ShouldBe(IdempotencyState.InProgress);
        (await store.TryBeginAsync("user:x", key, "h2", Ct)).State.ShouldBe(IdempotencyState.Mismatch);

        await store.CompleteAsync("user:x", key, new StoredResponse(201, """{"id":1}""", "application/json"), Ct);
        var replay = await store.TryBeginAsync("user:x", key, "h1", Ct);
        replay.State.ShouldBe(IdempotencyState.Completed);
        replay.Response!.Body.ShouldBe("""{"id":1}""");
    }
}
