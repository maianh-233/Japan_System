using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Application.Common.Messaging;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using JPCenter.Modules.System.Application.Permissions.ExpireOverrides;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.System;

/// <summary>User story 02 — vai trò & phân quyền, duyệt 2 người, trên API + PostgreSQL thật với dữ liệu mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed class PermissionFlowTests(SeededApiFactory api)
{
    /// <summary>Email nhân viên do test này tự tạo — AuthFlowTests bỏ qua các nhân viên này.</summary>
    public const string EmployeePrefix = "pq.";

    private const string SuperAdmin = "superadmin@jpcenter.vn";
    private const string Admin = "admin@jpcenter.vn";
    private const string Sale = "sale@jpcenter.vn";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------- Hỗ trợ ----------

    private async Task<HttpClient> SignIn(string email)
    {
        var client = api.CreateClient();
        var r = await client.PostAsJsonAsync("/api/v1/system/auth/login",
            new { identifier = email, password = SeededApiFactory.Password, remember = false }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await Json(r)).GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement;

    private static async Task<string> Code(HttpResponseMessage r) => (await Json(r)).GetProperty("code").GetString()!;

    private static async Task<JsonElement> Ok(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.IsSuccessStatusCode.ShouldBeTrue(body);
        return JsonDocument.Parse(body).RootElement;
    }

    /// <summary>
    /// Mỗi test dùng 1 tài khoản mẫu riêng (cố định theo email) — không trùng tài khoản demo và tài khoản AuthFlowTests dùng
    /// (AuthFlowTests lấy 8 tài khoản đầu theo thứ tự email).
    /// </summary>
    private async Task<UserAccount> Target(string email)
    {
        await using var scope = api.Scope();
        return await scope.ServiceProvider.GetRequiredService<SystemDbContext>().UserAccounts.AsNoTracking().FirstAsync(a => a.Email == email, Ct);
    }

    private static bool IsNull(JsonElement e, string name) => !e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null;

    private async Task<UserAccount> Reload(UserAccountId id)
    {
        await using var scope = api.Scope();
        return await scope.ServiceProvider.GetRequiredService<SystemDbContext>().UserAccounts.AsNoTracking().FirstAsync(a => a.Id == id, Ct);
    }

    private static Guid Branch(string seedId) => SeedData.IdOf(seedId);

    private static Guid Role(string seedId) => SeedData.IdOf(seedId);

    // ---------- PQ-01 / PQ-10 ----------

    [Fact]
    public async Task Danh_muc_quyen_va_danh_sach_vai_tro()
    {
        var sa = await SignIn(SuperAdmin);
        var catalog = await Ok(await sa.GetAsync("/api/v1/system/permissions/catalog", Ct));
        catalog.GetArrayLength().ShouldBe(9);

        var roles = await Ok(await sa.GetAsync("/api/v1/system/roles", Ct));
        var super = roles.EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Super Admin");
        super.GetProperty("superAdmin").GetBoolean().ShouldBeTrue();
        super.GetProperty("userCount").GetInt32().ShouldBe(1);

        // PQ-10.E1: Sale không có quyền trang Vai trò → 403
        var sale = await SignIn(Sale);
        (await sale.GetAsync("/api/v1/system/roles", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await sale.GetAsync("/api/v1/system/permissions/catalog", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------- PQ-02 → PQ-04 ----------

    [Fact]
    public async Task Tao_sua_xoa_vai_tro()
    {
        var sa = await SignIn(SuperAdmin);

        // PQ-02.E1 trùng tên (không phân biệt hoa thường)
        var dup = await sa.PostAsJsonAsync("/api/v1/system/roles", new { name = "lễ tân" }, Ct);
        dup.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(dup)).ShouldBe("system.role.duplicate_name");

        // PQ-03.E4: có update mà chưa có view → tự thêm view cùng phạm vi
        var created = await sa.PostAsJsonAsync("/api/v1/system/roles",
            new { name = "Thực tập sinh Sale", description = "Test", grants = new Dictionary<string, string> { ["crm.leads.update"] = "own" } }, Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var role = (await Json(created)).GetProperty("role");
        var id = role.GetProperty("id").GetGuid();
        role.GetProperty("grants").GetProperty("crm.leads.view").GetString().ShouldBe("own");

        // PQ-03: lưu ma trận theo version; lưu lại bằng version cũ → 409 (E6)
        var version = role.GetProperty("version").GetUInt32();
        var grants = new Dictionary<string, string> { ["crm.leads.view"] = "branch", ["crm.leads.export"] = "branch" };
        var saved = await Ok(await sa.PutAsJsonAsync($"/api/v1/system/roles/{id}/grants", new { grants, version }, Ct));
        saved.GetProperty("role").GetProperty("grants").GetProperty("crm.leads.export").GetString().ShouldBe("branch");
        saved.GetProperty("role").GetProperty("grants").TryGetProperty("crm.leads.update", out _).ShouldBeFalse();
        var stale = await sa.PutAsJsonAsync($"/api/v1/system/roles/{id}/grants", new { grants, version }, Ct);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(stale)).ShouldBe("system.concurrency");

        // Đổi tên
        var renamed = await Ok(await sa.PutAsJsonAsync($"/api/v1/system/roles/{id}", new { name = "TTS Sale", description = "Đổi" }, Ct));
        renamed.GetProperty("name").GetString().ShouldBe("TTS Sale");

        // PQ-04: xóa → Thùng rác
        (await sa.DeleteAsync($"/api/v1/system/roles/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
        (await db.TrashItems.AnyAsync(t => t.Kind == "system.role" && t.EntityId == id, Ct)).ShouldBeTrue();
        (await db.Roles.AnyAsync(r => r.Id == new RoleId(id), Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Vai_tro_mac_dinh_va_dang_dung_khong_xoa_duoc()
    {
        var sa = await SignIn(SuperAdmin);

        var superAdmin = await sa.PutAsJsonAsync($"/api/v1/system/roles/{Role("R-SA")}/grants",
            new { grants = new Dictionary<string, string>(), version = 0u }, Ct);
        (await Code(superAdmin)).ShouldBe("system.role.super_admin_locked");

        var system = await sa.DeleteAsync($"/api/v1/system/roles/{Role("R-GV")}", Ct);
        (await Code(system)).ShouldBe("system.role.system_role");

        var rename = await sa.PutAsJsonAsync($"/api/v1/system/roles/{Role("R-GV")}", new { name = "Giáo vụ 2" }, Ct);
        (await Code(rename)).ShouldBe("system.role.system_role");

        // R-LT (Lễ tân) là role tự tạo, đang có 2 người dùng
        var inUse = await sa.DeleteAsync($"/api/v1/system/roles/{Role("R-LT")}", Ct);
        inUse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(inUse)).ShouldBe("system.role.in_use");
    }

    // ---------- PQ-05 / PQ-09 ----------

    [Fact]
    public async Task Gan_vai_tro_tai_chinh_phai_qua_duyet_2_nguoi()
    {
        var target = await Target("vy.vy@jpcenter.vn");
        var sa = await SignIn(SuperAdmin);

        var r = await Ok(await sa.PutAsJsonAsync($"/api/v1/system/users/{target.Id.Value}/roles",
            new { roles = new[] { new { roleId = Role("R-KT"), branchIds = new[] { Branch("cg") } } } }, Ct));
        var approvalId = r.GetProperty("pendingApprovalId").GetGuid();
        (await Reload(target.Id)).Roles.Single().RoleId.ShouldBe(new RoleId(Role("R-SALE"))); // chưa áp dụng

        // E1: người đề nghị không tự duyệt
        var self = await sa.PostAsJsonAsync($"/api/v1/system/approvals/{approvalId}/approve", new { note = (string?)null }, Ct);
        (await Code(self)).ShouldBe("system.approval.self_approval");

        // Admin (xem được Nhật ký) thấy và duyệt được
        var admin = await SignIn(Admin);
        var pending = await Ok(await admin.GetAsync("/api/v1/system/approvals?status=pending", Ct));
        var item = pending.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == approvalId);
        item.GetProperty("kind").GetString().ShouldBe("finance_permission");
        item.GetProperty("canDecide").GetBoolean().ShouldBeTrue();

        var approved = await Ok(await admin.PostAsJsonAsync($"/api/v1/system/approvals/{approvalId}/approve", new { note = "OK" }, Ct));
        approved.GetProperty("request").GetProperty("status").GetString().ShouldBe("approved");
        var after = await Reload(target.Id);
        after.Roles.Single().RoleId.ShouldBe(new RoleId(Role("R-KT")));
        after.Roles.Single().BranchIds.ShouldBe([new BranchId(Branch("cg"))]);

        // E3: duyệt lại
        var again = await admin.PostAsJsonAsync($"/api/v1/system/approvals/{approvalId}/approve", new { note = "OK" }, Ct);
        (await Code(again)).ShouldBe("system.approval.not_pending");
    }

    [Fact]
    public async Task Gan_vai_tro_khong_tai_chinh_ap_dung_ngay_va_khong_tu_sua_minh()
    {
        var target = await Target("tung.tung@jpcenter.vn");
        var sa = await SignIn(SuperAdmin);

        var r = await Ok(await sa.PutAsJsonAsync($"/api/v1/system/users/{target.Id.Value}/roles",
            new { roles = new[] { new { roleId = Role("R-SALE"), branchIds = new[] { Branch("q1") } }, new { roleId = Role("R-SALE"), branchIds = new[] { Branch("cg") } } } }, Ct));
        IsNull(r, "pendingApprovalId").ShouldBeTrue();
        // E4: cùng role 2 dòng → gộp chi nhánh
        var role = r.GetProperty("user").GetProperty("roles").EnumerateArray().Single();
        role.GetProperty("branchIds").GetArrayLength().ShouldBe(2);

        // E1: tự sửa mình
        var me = (await Ok(await sa.GetAsync("/api/v1/system/users", Ct))).EnumerateArray()
            .Single(u => u.GetProperty("email").GetString() == SuperAdmin).GetProperty("accountId").GetGuid();
        var self = await sa.PutAsJsonAsync($"/api/v1/system/users/{me}/roles", new { roles = Array.Empty<object>() }, Ct);
        (await Code(self)).ShouldBe("system.permission.self_edit");

        // Sai quyền: Sale không vào được Phân quyền người dùng
        var sale = await SignIn(Sale);
        (await sale.PutAsJsonAsync($"/api/v1/system/users/{target.Id.Value}/roles", new { roles = Array.Empty<object>() }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------- PQ-06 / PQ-07 ----------

    [Fact]
    public async Task Quyen_le_cap_chan_go_va_quyen_hieu_luc()
    {
        var target = await Target("nhu.nhu@jpcenter.vn");
        var sa = await SignIn(SuperAdmin);
        var url = $"/api/v1/system/users/{target.Id.Value}";

        var past = await sa.PostAsJsonAsync($"{url}/overrides",
            new { perm = "crm.leads.export", effect = "grant", scope = "own", until = DateTimeOffset.UtcNow.AddDays(-1), reason = "Test" }, Ct);
        (await Code(past)).ShouldBe("system.permission.invalid_until");

        var noReason = await sa.PostAsJsonAsync($"{url}/overrides", new { perm = "crm.leads.export", effect = "deny", reason = "" }, Ct);
        noReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Chặn quyền xem lead (Sale có qua role)
        var denied = await Ok(await sa.PostAsJsonAsync($"{url}/overrides",
            new { perm = "crm.leads.view", effect = "deny", until = DateTimeOffset.UtcNow.AddDays(7), reason = "Tạm khóa" }, Ct));
        var overrideId = denied.GetProperty("user").GetProperty("overrides")[0].GetProperty("id").GetGuid();

        var eff = await Ok(await sa.GetAsync($"{url}/effective-permissions", Ct));
        var leads = eff.GetProperty("permissions").EnumerateArray().Single(p => p.GetProperty("perm").GetString() == "crm.leads.view");
        leads.GetProperty("denied").GetProperty("reason").GetString().ShouldBe("Tạm khóa");
        leads.GetProperty("sources").EnumerateArray().Select(s => s.GetString()).ShouldContain("Sale");

        // Cấp lẻ quyền đã có từ role → cảnh báo (E6)
        var granted = await Ok(await sa.PostAsJsonAsync($"{url}/overrides",
            new { perm = "crm.leads.update", effect = "grant", scope = "all", reason = "Hỗ trợ chi nhánh khác" }, Ct));
        granted.GetProperty("warnings").GetArrayLength().ShouldBe(1);

        // Cấp lẻ quyền tài chính → chờ duyệt (E3)
        var finance = await Ok(await sa.PostAsJsonAsync($"{url}/overrides",
            new { perm = "finance.invoices.view", effect = "grant", scope = "own", reason = "Đối soát" }, Ct));
        finance.GetProperty("pendingApprovalId").ValueKind.ShouldBe(JsonValueKind.String);

        // Gỡ chặn (E7)
        (await sa.DeleteAsync($"{url}/overrides/{overrideId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Reload(target.Id)).Overrides.ShouldNotContain(o => o.Id == overrideId);
    }

    // ---------- PQ-08 ----------

    [Fact]
    public async Task Khoa_tai_khoan_thu_hoi_phien_ngay_roi_mo_khoa()
    {
        var target = await Target("yuki.t.sato@jpcenter.vn");
        var victim = await SignIn(target.Email);
        (await victim.GetAsync("/api/v1/system/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var sa = await SignIn(SuperAdmin);
        var noReason = await sa.PostAsJsonAsync($"/api/v1/system/users/{target.Id.Value}/lock", new { reason = "" }, Ct);
        noReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var locked = await Ok(await sa.PostAsJsonAsync($"/api/v1/system/users/{target.Id.Value}/lock", new { reason = "Nghỉ phép dài" }, Ct));
        locked.GetProperty("status").GetString().ShouldBe("locked");
        (await victim.GetAsync("/api/v1/system/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var unlocked = await Ok(await sa.PostAsync($"/api/v1/system/users/{target.Id.Value}/unlock", null, Ct));
        unlocked.GetProperty("status").GetString().ShouldBe("active");
        (await SignIn(target.Email)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Cap_tai_khoan_cho_nhan_vien_moi()
    {
        var email = $"{EmployeePrefix}{Guid.NewGuid():N}"[..12] + "@jpcenter.vn";
        Guid employeeId;
        await using (var scope = api.Scope())
        {
            var hr = scope.ServiceProvider.GetRequiredService<HrDbContext>();
            var employee = Employee.Create($"NV-PQ{Random.Shared.Next(1000, 9999)}", "Nhân viên Test PQ", Gender.Female, null, "0900000000", email,
                new BranchId(Branch("cg")), EmployeeKind.Staff, Department.Sales, "Sale", false, new DateOnly(2026, 11, 1), null,
                new EmploymentContract(ContractType.Probation, "HD-PQ", new DateOnly(2026, 11, 1), null, null, null), 0).Value;
            hr.Employees.Add(employee);
            await hr.SaveChangesAsync(Ct);
            employeeId = employee.Id.Value;
        }

        var sa = await SignIn(SuperAdmin);
        var without = await Ok(await sa.GetAsync("/api/v1/system/users/employees-without-account", Ct));
        without.EnumerateArray().ShouldContain(e => e.GetProperty("id").GetGuid() == employeeId);

        // Sai quyền
        var sale = await SignIn(Sale);
        (await sale.PostAsJsonAsync("/api/v1/system/users", new { employeeId, roles = new[] { new { roleId = Role("R-SALE"), branchIds = new[] { Branch("cg") } } } }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var created = await sa.PostAsJsonAsync("/api/v1/system/users",
            new { employeeId, roles = new[] { new { roleId = Role("R-SALE"), branchIds = new[] { Branch("cg") } } } }, Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var user = (await Json(created)).GetProperty("user");
        user.GetProperty("status").GetString().ShouldBe("invited");
        api.Emails.Sent.ShouldContain(m => m.To == email);

        var again = await sa.PostAsJsonAsync("/api/v1/system/users",
            new { employeeId, roles = new[] { new { roleId = Role("R-SALE"), branchIds = Array.Empty<Guid>() } } }, Ct);
        (await Code(again)).ShouldBe("system.user.exists");

        var resend = await sa.PostAsync($"/api/v1/system/users/{user.GetProperty("accountId").GetGuid()}/resend-invite", null, Ct);
        resend.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // ---------- PQ-12 ----------

    [Fact]
    public async Task Job_bao_quyen_le_sap_het_han_cho_nguoi_cap()
    {
        var target = await Target("thu.t.thu@jpcenter.vn");
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
        var granter = await db.UserAccounts.FirstAsync(a => a.Email == SuperAdmin, Ct);
        var account = await db.UserAccounts.FirstAsync(a => a.Id == target.Id, Ct);
        var now = DateTimeOffset.UtcNow;
        account.AddOverride("crm.leads.export", OverrideEffect.Grant, PermissionScope.Own, now.AddDays(2), "Xuất báo cáo tuần", granter.Id.Value, now);
        await db.SaveChangesAsync(Ct);

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ExpireOverridesCommand(), Ct);
        result.Value.Reminded.ShouldBeGreaterThanOrEqualTo(1);
        api.Emails.Sent.ShouldContain(m => m.To == SuperAdmin && m.Subject.Contains("sắp hết hạn"));
    }
}
