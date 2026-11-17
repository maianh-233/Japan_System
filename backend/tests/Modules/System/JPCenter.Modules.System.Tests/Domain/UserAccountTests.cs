using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Tests.Domain;

public sealed class UserAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 3, 0, 0, TimeSpan.Zero);

    private static UserAccount Invited() => UserAccount.Invite(Guid.NewGuid(), "  NhanVien@JPCenter.vn ").Value;

    private static UserAccount Active()
    {
        var a = Invited();
        a.SetPassword("hash");
        return a;
    }

    [Fact]
    public void Moi_thi_email_chuan_hoa_va_trang_thai_da_moi()
    {
        var a = Invited();
        a.Email.ShouldBe("nhanvien@jpcenter.vn");
        a.Status.ShouldBe(UserAccountStatus.Invited);
        a.CanSignIn(Now).Error.Code.ShouldBe("auth.account_invited");
    }

    [Fact]
    public void Thieu_email_thi_loi() =>
        UserAccount.Invite(Guid.NewGuid(), " ").Error.ShouldBe(UserAccountErrors.EmailRequired);

    [Fact]
    public void Dat_mat_khau_lan_dau_thi_kich_hoat_va_thu_hoi_token()
    {
        var a = Invited();
        var tv = a.TokenVersion;
        a.SetPassword("hash");
        a.Status.ShouldBe(UserAccountStatus.Active);
        a.TokenVersion.ShouldBe(tv + 1);
        a.CanSignIn(Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Mat_khau_tam_buoc_doi_doi_xong_thi_bo_co()
    {
        var a = Active();
        a.SetTemporaryPassword("tmp");
        a.MustChangePassword.ShouldBeTrue();
        a.SetPassword("new");
        a.MustChangePassword.ShouldBeFalse();
    }

    [Fact]
    public void Sai_5_lan_thi_khoa_tam_dang_nhap_dung_thi_xoa_bo_dem()
    {
        var a = Active();
        for (var i = 0; i < 4; i++) a.RecordFailedLogin(Now).ShouldBeFalse();
        a.RecordFailedLogin(Now).ShouldBeTrue();
        a.CanSignIn(Now).Error.Code.ShouldBe("auth.too_many_attempts");
        a.CanSignIn(Now.AddMinutes(16)).IsSuccess.ShouldBeTrue();

        a.RecordFailedLogin(Now.AddMinutes(16));
        a.RecordLogin(Now.AddMinutes(17));
        a.Throttle.FailedCount.ShouldBe(0);
    }

    [Fact]
    public void Khoa_thu_hoi_token_khoa_lan_2_thi_loi_mo_khoa_xoa_ly_do()
    {
        var a = Active();
        var tv = a.TokenVersion;
        a.Lock("Nghỉ phép").IsSuccess.ShouldBeTrue();
        a.TokenVersion.ShouldBe(tv + 1);
        a.CanSignIn(Now).Error.ShouldBe(UserAccountErrors.AccountLocked);
        a.Lock("Lần 2").Error.ShouldBe(UserAccountErrors.AlreadyLocked);

        a.Unlock();
        a.Status.ShouldBe(UserAccountStatus.Active);
        a.LockedReason.ShouldBeNull();
    }

    [Fact]
    public void Khoa_khi_con_da_moi_thi_mo_khoa_ve_da_moi()
    {
        var a = Invited();
        a.Lock("Nhầm người");
        a.Unlock();
        a.Status.ShouldBe(UserAccountStatus.Invited);
    }

    [Fact]
    public void Gan_vai_tro_cung_role_2_dong_thi_gop_chi_nhanh()
    {
        var a = Active();
        var role = RoleId.New();
        BranchId b1 = BranchId.New(), b2 = BranchId.New();
        a.SetRoles([(role, [b1]), (role, [b2, b1])]);
        a.Roles.Count.ShouldBe(1);
        a.Roles[0].BranchIds.ShouldBe([b1, b2], ignoreOrder: true);
    }

    [Fact]
    public void Mot_dong_moi_chi_nhanh_thi_ca_role_la_moi_chi_nhanh()
    {
        var a = Active();
        var role = RoleId.New();
        a.SetRoles([(role, [BranchId.New()]), (role, [])]);
        a.Roles.Single().BranchIds.ShouldBeEmpty();
    }

    [Fact]
    public void Doi_vai_tro_va_quyen_le_deu_thu_hoi_token()
    {
        var a = Active();
        var tv = a.TokenVersion;
        a.SetRoles([]);
        var o = a.AddOverride("crm.leads.export", OverrideEffect.Grant, PermissionScope.Own, null, "Test", Guid.NewGuid(), Now);
        a.RemoveOverride(o.Id);
        a.TokenVersion.ShouldBe(tv + 3);

        a.RemoveOverride(Guid.NewGuid()); // không có → không đổi
        a.TokenVersion.ShouldBe(tv + 3);
    }
}
