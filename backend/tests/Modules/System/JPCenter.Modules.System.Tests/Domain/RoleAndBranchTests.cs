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

    private static readonly DateOnly Today = new(2026, 11, 17);

    private static Branch NewBranch(string code = "cg", string? prefix = null) =>
        Branch.Create(code, "Cầu Giấy", "Số 1", "0900000000", "cg@jpcenter.vn", prefix, Today.AddDays(30)).Value;

    [Fact]
    public void Chi_nhanh_moi_chuan_hoa_ma_va_tien_to_mac_dinh_theo_ma()
    {
        Branch.Create("", "Cầu Giấy", "", "", "", null, Today).Error.ShouldBe(BranchErrors.CodeRequired);
        Branch.Create("cg", " ", "", "", "", null, Today).Error.ShouldBe(BranchErrors.NameRequired);
        Branch.Create("C", "X", "", "", "", null, Today).Error.ShouldBe(BranchErrors.InvalidCode);
        Branch.Create("CGIAY", "X", "", "", "", null, Today).Error.ShouldBe(BranchErrors.InvalidCode);
        Branch.Create("C-G", "X", "", "", "", null, Today).Error.ShouldBe(BranchErrors.InvalidCode);
        Branch.Create("cg", "X", "", "", "", "HD-1", Today).Error.ShouldBe(BranchErrors.InvalidInvoicePrefix);

        var b = NewBranch(" cg ");
        (b.Code, b.InvoicePrefix, b.Status).ShouldBe(("CG", "CG", BranchStatus.Preparing));
        NewBranch(prefix: "cgy").InvoicePrefix.ShouldBe("CGY");
    }

    [Fact]
    public void Tai_khoan_ngan_hang_nhap_du_ca_3_hoac_bo_trong()
    {
        var b = NewBranch();
        b.SetBankAccount("Vietcombank", "0123", null).Error.ShouldBe(BranchErrors.BankIncomplete);
        b.SetBankAccount("Vietcombank", "12ab5678", "A").Error.ShouldBe(BranchErrors.BankAccountInvalid);
        b.SetBankAccount("Vietcombank", "12345", "A").Error.ShouldBe(BranchErrors.BankAccountInvalid);

        b.SetBankAccount("Vietcombank", "0011 0022 33", "cong ty jp").IsSuccess.ShouldBeTrue();
        (b.BankAccount, b.BankHolder).ShouldBe(("0011002233", "CONG TY JP"));
        b.SetBankAccount(" ", "", null).IsSuccess.ShouldBeTrue();
        b.HasBankAccount.ShouldBeFalse();
    }

    [Fact]
    public void Khai_truong_som_thi_ngay_khai_truong_la_hom_nay()
    {
        var b = NewBranch();
        b.Open(Today).IsSuccess.ShouldBeTrue();
        (b.Status, b.OpenedAt).ShouldBe((BranchStatus.Active, Today));
        b.Open(Today).Error.ShouldBe(BranchErrors.InvalidStatus);
    }

    [Fact]
    public void Dong_tu_dang_chuan_bi_hoac_hoat_dong_mo_lai_chi_tu_da_dong()
    {
        var preparing = NewBranch();
        preparing.Reopen().Error.ShouldBe(BranchErrors.InvalidStatus);
        preparing.Close().IsSuccess.ShouldBeTrue();
        preparing.Close().Error.ShouldBe(BranchErrors.InvalidStatus);

        var b = NewBranch();
        b.Open(Today);
        b.Close().IsSuccess.ShouldBeTrue();
        b.Reopen().IsSuccess.ShouldBeTrue();
        b.Status.ShouldBe(BranchStatus.Active);
    }

    [Fact]
    public void Sua_khong_doi_ma_tien_to_trong_thi_theo_ma()
    {
        var b = NewBranch(prefix: "XY");
        b.Update(" ", "a", "p", "e", null, Today).Error.ShouldBe(BranchErrors.NameRequired);
        b.Update("Cầu Giấy 2", "a", "p", "e", null, Today).IsSuccess.ShouldBeTrue();
        (b.Code, b.Name, b.InvoicePrefix).ShouldBe(("CG", "Cầu Giấy 2", "CG"));
    }
}
