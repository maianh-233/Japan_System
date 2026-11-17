using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Tests.Application;

/// <summary>Quy tắc vàng 1 (chỉ cấp quyền mình có), tự thêm Xem, phát hiện thêm quyền tài chính (duyệt 2 người).</summary>
public sealed class GrantRulesTests
{
    private static readonly Guid Cg = Guid.NewGuid(), Q1 = Guid.NewGuid();

    private sealed class FakeCatalog : IPermissionCatalog
    {
        public IReadOnlyList<PermissionModule> Modules { get; } =
        [
            new("crm", "CRM", [new PermissionGroup("crm.leads", "Lead", ["view", "update", "export"])]),
            new("finance", "Tài chính", [new PermissionGroup("finance.invoices", "Hóa đơn", ["view", "update"])]),
        ];

        public IReadOnlySet<string> All => Modules.SelectMany(m => m.Groups).SelectMany(g => g.Actions.Select(a => $"{g.Key}.{a}")).ToHashSet();

        public bool IsValid(string permission) => All.Contains(permission);
    }

    private static readonly FakeCatalog Catalog = new();

    private static UserAccess Granter(bool superAdmin = false, bool allBranches = false, IReadOnlyList<Guid>? branches = null,
        params (string Perm, PermissionScope Scope)[] perms) =>
        new(Guid.NewGuid(), Portal.Staff, 1, true, "A", "Role", Guid.NewGuid(), null, superAdmin, allBranches, branches ?? [],
            perms.ToDictionary(p => p.Perm, p => new EffectivePermission(p.Scope, [])));

    [Fact]
    public void Chi_cap_duoc_quyen_minh_co_voi_pham_vi_khong_rong_hon()
    {
        var c = new GrantChecker(Granter(perms: ("crm.leads.view", PermissionScope.Branch)));
        c.CanGrant("crm.leads.view", PermissionScope.Own).ShouldBeTrue();
        c.CanGrant("crm.leads.view", PermissionScope.Branch).ShouldBeTrue();
        c.CanGrant("crm.leads.view", PermissionScope.All).ShouldBeFalse();
        c.CanGrant("crm.leads.export", PermissionScope.Own).ShouldBeFalse();

        c.Exceeding(new Dictionary<string, PermissionScope> { ["crm.leads.view"] = PermissionScope.All, ["crm.leads.update"] = PermissionScope.Own })
            .ShouldBe(["crm.leads.view", "crm.leads.update"], ignoreOrder: true);
    }

    [Fact]
    public void Super_admin_cap_duoc_moi_thu()
    {
        var c = new GrantChecker(Granter(superAdmin: true));
        c.CanGrant("finance.invoices.update", PermissionScope.All).ShouldBeTrue();
        c.CanUseBranches([]).ShouldBeTrue();
    }

    [Fact]
    public void Nguoi_gan_theo_chi_nhanh_chi_chon_chi_nhanh_cua_minh_khong_chon_moi_chi_nhanh()
    {
        var c = new GrantChecker(Granter(branches: [Cg]));
        c.CanUseBranches([Cg]).ShouldBeTrue();
        c.CanUseBranches([Cg, Q1]).ShouldBeFalse();
        c.CanUseBranches([]).ShouldBeFalse();

        new GrantChecker(Granter(allBranches: true)).CanUseBranches([]).ShouldBeTrue();
    }

    [Fact]
    public void Co_sua_ma_chua_co_xem_thi_tu_them_xem_cung_pham_vi()
    {
        var result = PermissionChanges.WithImpliedView(new Dictionary<string, PermissionScope>
        {
            ["crm.leads.update"] = PermissionScope.Branch,
            ["crm.leads.export"] = PermissionScope.All,
        }, Catalog);
        result["crm.leads.view"].ShouldBe(PermissionScope.All);

        // Đã có Xem rộng hơn thì giữ nguyên
        PermissionChanges.WithImpliedView(new Dictionary<string, PermissionScope>
        {
            ["crm.leads.view"] = PermissionScope.All,
            ["crm.leads.update"] = PermissionScope.Own,
        }, Catalog)["crm.leads.view"].ShouldBe(PermissionScope.All);
    }

    [Fact]
    public void Gop_o_quyen_vao_ma_tran_o_rong_la_bo_quyen()
    {
        var role = Role.Create("Kế toán", "").Value;
        role.SetGrants(new Dictionary<string, PermissionScope> { ["crm.leads.view"] = PermissionScope.Own, ["crm.leads.export"] = PermissionScope.Own });
        PermissionChanges.MergeGrants(role, new Dictionary<string, string?> { ["crm.leads.view"] = "all", ["crm.leads.export"] = null });
        role.Grants.Single().ShouldSatisfyAllConditions(
            g => g.Permission.ShouldBe("crm.leads.view"),
            g => g.Scope.ShouldBe(PermissionScope.All));
    }

    [Fact]
    public void Phat_hien_them_hoac_noi_quyen_tai_chinh()
    {
        static EffectiveGrant G(string perm, PermissionScope? scope) => new(perm, scope, [], ["X"], null);
        var before = new Dictionary<string, EffectiveGrant> { ["finance.invoices.view"] = G("finance.invoices.view", PermissionScope.Own) };

        // Nới phạm vi + thêm quyền mới
        RoleGrants.FinanceAdded(before, false, new Dictionary<string, EffectiveGrant>
        {
            ["finance.invoices.view"] = G("finance.invoices.view", PermissionScope.Branch),
            ["finance.invoices.update"] = G("finance.invoices.update", PermissionScope.Own),
            ["crm.leads.view"] = G("crm.leads.view", PermissionScope.All),
        }, false, Catalog).ShouldBe(["finance.invoices.update", "finance.invoices.view"]);

        // Thu hẹp / giữ nguyên → không cần duyệt
        RoleGrants.FinanceAdded(before, false, new Dictionary<string, EffectiveGrant>(), false, Catalog).ShouldBeEmpty();

        // Trở thành Super Admin → có thêm mọi quyền tài chính; đã là Super Admin → không thêm gì
        RoleGrants.FinanceAdded(before, false, new Dictionary<string, EffectiveGrant>(), true, Catalog)
            .ShouldBe(["finance.invoices.update", "finance.invoices.view"]);
        RoleGrants.FinanceAdded(before, true, new Dictionary<string, EffectiveGrant>(), true, Catalog).ShouldBeEmpty();
    }

    [Fact]
    public void Bi_chan_thi_khong_tinh_la_dang_co_quyen_tai_chinh()
    {
        var a = UserAccount.Invite(Guid.NewGuid(), "x@jpcenter.vn").Value;
        var deny = a.AddOverride("finance.invoices.view", OverrideEffect.Deny, null, null, "Khóa", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var before = new Dictionary<string, EffectiveGrant>
        {
            ["finance.invoices.view"] = new("finance.invoices.view", PermissionScope.Own, [], ["Kế toán"], deny),
        };
        var after = new Dictionary<string, EffectiveGrant>
        {
            ["finance.invoices.view"] = new("finance.invoices.view", PermissionScope.Own, [], ["Kế toán"], null),
        };
        RoleGrants.FinanceAdded(before, false, after, false, Catalog).ShouldBe(["finance.invoices.view"]);
    }
}
