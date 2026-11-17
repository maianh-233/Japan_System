using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Tests.Domain;

/// <summary>PQ-07 — thuật toán giống <c>frontend/src/features/system/access.ts › effectivePerms</c>.</summary>
public sealed class EffectivePermissionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 3, 0, 0, TimeSpan.Zero);
    private static readonly BranchId Cg = BranchId.New(), Q1 = BranchId.New();

    private static Role RoleWith(string name, params (string Perm, PermissionScope Scope)[] grants)
    {
        var r = Role.Create(name, "").Value;
        r.SetGrants(grants.ToDictionary(g => g.Perm, g => g.Scope));
        return r;
    }

    private static (UserAccount Account, Dictionary<RoleId, Role> Roles) Setup(params (Role Role, BranchId[] Branches)[] assignments)
    {
        var a = UserAccount.Invite(Guid.NewGuid(), "x@jpcenter.vn").Value;
        a.SetRoles(assignments.Select(x => (x.Role.Id, (IReadOnlyCollection<BranchId>)x.Branches)));
        return (a, assignments.ToDictionary(x => x.Role.Id, x => x.Role));
    }

    private static IReadOnlyDictionary<string, EffectiveGrant> Compute(UserAccount a, Dictionary<RoleId, Role> roles) =>
        EffectivePermissions.Compute(a.Roles, roles, a.Overrides, Now);

    [Fact]
    public void Hai_role_khac_pham_vi_lay_rong_nhat_va_liet_ke_ca_2_nguon()
    {
        var sale = RoleWith("Sale", ("crm.leads.view", PermissionScope.Own));
        var lead = RoleWith("Trưởng nhóm", ("crm.leads.view", PermissionScope.Branch));
        var (a, roles) = Setup((sale, []), (lead, [Cg]));

        var g = Compute(a, roles)["crm.leads.view"];
        g.Scope.ShouldBe(PermissionScope.Branch);
        g.Sources.ShouldBe(["Sale", "Trưởng nhóm"], ignoreOrder: true);
    }

    [Fact]
    public void Role_gan_vai_chi_nhanh_thi_toan_he_thong_thu_ve_chi_nhanh_tru_module_system()
    {
        var role = RoleWith("Quản lý", ("crm.leads.view", PermissionScope.All), ("system.audit.view", PermissionScope.All));
        var (a, roles) = Setup((role, [Cg, Q1]));

        var result = Compute(a, roles);
        result["crm.leads.view"].Scope.ShouldBe(PermissionScope.Branch);
        result["crm.leads.view"].BranchIds.ShouldBe([Cg, Q1], ignoreOrder: true);
        result["system.audit.view"].Scope.ShouldBe(PermissionScope.All);
    }

    [Fact]
    public void Chan_le_con_han_thang_moi_role_het_han_thi_quyen_role_dung_lai()
    {
        var sale = RoleWith("Sale", ("crm.leads.view", PermissionScope.Own));
        var (a, roles) = Setup((sale, []));
        a.AddOverride("crm.leads.view", OverrideEffect.Deny, null, Now.AddDays(1), "Tạm khóa", Guid.NewGuid(), Now);

        Compute(a, roles)["crm.leads.view"].UsableScope.ShouldBeNull();
        EffectivePermissions.Compute(a.Roles, roles, a.Overrides, Now.AddDays(2))["crm.leads.view"].UsableScope.ShouldBe(PermissionScope.Own);
    }

    [Fact]
    public void Cap_le_cong_them_het_han_thi_khong_tinh()
    {
        var (a, roles) = Setup();
        a.AddOverride("crm.leads.export", OverrideEffect.Grant, PermissionScope.Own, Now.AddDays(7), "Báo cáo", Guid.NewGuid(), Now);
        a.AddOverride("finance.debts.view", OverrideEffect.Grant, PermissionScope.All, Now.AddDays(-1), "Cũ", Guid.NewGuid(), Now.AddDays(-8));

        var result = Compute(a, roles);
        result["crm.leads.export"].Sources.ShouldBe(["Cấp lẻ"]);
        result.ContainsKey("finance.debts.view").ShouldBeFalse();
    }

    [Fact]
    public void Super_admin_nhan_ra_qua_role()
    {
        var sa = Role.Create("Super Admin", "", true, true).Value;
        var (a, roles) = Setup((sa, []));
        EffectivePermissions.IsSuperAdmin(a.Roles, roles).ShouldBeTrue();
        Compute(a, roles).ShouldBeEmpty(); // toàn quyền xử lý riêng, không liệt kê
    }
}
