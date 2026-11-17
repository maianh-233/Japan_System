using JPCenter.Modules.System.Domain.Branches;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Tests.Domain;

public sealed class RoleAndBranchTests
{
    [Fact]
    public void Tao_role_can_ten() => Role.Create(" ", "").Error.ShouldBe(RoleErrors.NameRequired);

    [Fact]
    public void Super_admin_khong_sua_ma_tran()
    {
        var role = Role.Create("Super Admin", "", isSystem: true, isSuperAdmin: true).Value;
        role.SetGrants(new Dictionary<string, PermissionScope> { ["crm.leads.view"] = PermissionScope.All })
            .Error.ShouldBe(RoleErrors.SuperAdminLocked);
    }

    [Fact]
    public void Dat_ma_tran_thay_toan_bo()
    {
        var role = Role.Create("Lễ tân", "").Value;
        role.SetGrants(new Dictionary<string, PermissionScope> { ["crm.leads.view"] = PermissionScope.Own });
        role.SetGrants(new Dictionary<string, PermissionScope> { ["crm.leads.export"] = PermissionScope.Branch });
        role.Grants.Single().Permission.ShouldBe("crm.leads.export");
    }

    [Fact]
    public void Role_mac_dinh_khong_xoa_khong_doi_ten_nhung_sua_duoc_mo_ta()
    {
        var role = Role.Create("Giáo vụ", "cũ", isSystem: true).Value;
        role.EnsureDeletable().Error.ShouldBe(RoleErrors.SystemRole);
        role.Rename("Giáo vụ 2", "mới").Error.ShouldBe(RoleErrors.SystemRoleRename);
        role.Rename("Giáo vụ", " mới ").IsSuccess.ShouldBeTrue();
        role.Description.ShouldBe("mới");
    }

    [Fact]
    public void Role_tu_tao_doi_ten_va_xoa_duoc()
    {
        var role = Role.Create("Lễ tân", "").Value;
        role.Rename(" Lễ tân CG ", "").IsSuccess.ShouldBeTrue();
        role.Name.ShouldBe("Lễ tân CG");
        role.EnsureDeletable().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Chi_nhanh_moi_dang_chuan_bi_mo_roi_dong()
    {
        Branch.Create("", "Cầu Giấy", "", "", "", "CG", new DateOnly(2026, 1, 1)).Error.ShouldBe(BranchErrors.CodeRequired);
        Branch.Create("cg", " ", "", "", "", "CG", new DateOnly(2026, 1, 1)).Error.ShouldBe(BranchErrors.NameRequired);

        var b = Branch.Create(" cg ", "Cầu Giấy", "", "", "", "CG", new DateOnly(2026, 1, 1)).Value;
        b.Code.ShouldBe("CG");
        b.Status.ShouldBe(BranchStatus.Preparing);
        b.Activate().IsSuccess.ShouldBeTrue();
        b.Activate().Error.ShouldBe(BranchErrors.InvalidStatus);
        b.Close().IsSuccess.ShouldBeTrue();
        b.Close().Error.ShouldBe(BranchErrors.InvalidStatus);
    }
}
