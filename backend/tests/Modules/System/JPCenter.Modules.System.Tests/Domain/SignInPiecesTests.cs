using JPCenter.Modules.System.Domain.Approvals;
using JPCenter.Modules.System.Domain.Common;
using JPCenter.Modules.System.Domain.OneTimeTokens;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Sessions;

namespace JPCenter.Modules.System.Tests.Domain;

/// <summary>Khóa tạm, mã dùng 1 lần, phiên đăng nhập, tài khoản cổng, yêu cầu duyệt 2 người.</summary>
public sealed class SignInPiecesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 3, 0, 0, TimeSpan.Zero);

    // ---------- LoginThrottle ----------

    [Fact]
    public void Throttle_dang_khoa_thi_sai_them_khong_tang_dem_va_het_han_thi_mo()
    {
        var t = LoginThrottle.None;
        for (var i = 0; i < LoginThrottle.MaxFailures; i++) t = t.Fail(Now);
        t.IsLocked(Now).ShouldBeTrue();
        t.JustLocked(Now).ShouldBeTrue();

        var again = t.Fail(Now.AddMinutes(1));
        again.LockedUntil.ShouldBe(t.LockedUntil);
        again.IsLocked(Now.AddMinutes(LoginThrottle.LockMinutes + 1)).ShouldBeFalse();
    }

    // ---------- OneTimeToken ----------

    private static OneTimeToken Otp(Guid? account = null, int max = 3) =>
        OneTimeToken.Issue(TokenPurpose.PasswordResetOtp, AccountKind.Staff, account ?? Guid.NewGuid(), "a@b.vn", "H", Now, TimeSpan.FromMinutes(10), max);

    [Fact]
    public void Ma_dung_thi_dung_duoc_1_lan()
    {
        var t = Otp();
        t.Verify("H", Now).IsSuccess.ShouldBeTrue();
        t.Verify("H", Now).Error.ShouldBe(OneTimeTokenErrors.Expired);
    }

    [Fact]
    public void Ma_sai_bao_so_lan_con_lai_het_luot_thi_khoa_ke_ca_ma_dung()
    {
        var t = Otp(max: 2);
        t.Verify("X", Now).Error.Message.ShouldContain("Còn 1");
        t.Verify("X", Now).Error.ShouldBe(OneTimeTokenErrors.Locked);
        t.Verify("H", Now).Error.ShouldBe(OneTimeTokenErrors.Locked);
    }

    [Fact]
    public void Ma_het_han_hoac_bi_vo_hieu_thi_khong_dung_duoc()
    {
        Otp().Verify("H", Now.AddMinutes(11)).Error.ShouldBe(OneTimeTokenErrors.Expired);
        var t = Otp();
        t.Invalidate(Now);
        t.Verify("H", Now).Error.ShouldBe(OneTimeTokenErrors.Expired);
    }

    [Fact]
    public void Ma_gia_cho_email_khong_ton_tai_khong_bao_gio_dung()
    {
        var t = OneTimeToken.Issue(TokenPurpose.PasswordResetOtp, AccountKind.Staff, null, "x@y.vn", "H", Now, TimeSpan.FromMinutes(10));
        t.Verify("H", Now).IsFailure.ShouldBeTrue();
    }

    // ---------- UserSession ----------

    [Fact]
    public void Phien_xoay_vong_giu_token_cu_thu_hoi_thi_khong_xoay_duoc()
    {
        var s = UserSession.Start(AccountKind.Staff, Guid.NewGuid(), "T1", "Chrome", "1.1.1.1", false, Now, Now.AddHours(12));
        s.Rotate("T2", "2.2.2.2", Now.AddMinutes(5), Now.AddHours(12)).IsSuccess.ShouldBeTrue();
        (s.RefreshTokenHash, s.PreviousTokenHash, s.Ip).ShouldBe(("T2", "T1", "2.2.2.2"));

        s.Revoke("logout", Now.AddMinutes(6));
        s.Revoke("admin", Now.AddMinutes(7)); // lần 2 không ghi đè lý do
        s.RevokeReason.ShouldBe("logout");
        s.IsActive(Now.AddMinutes(8)).ShouldBeFalse();
        s.Rotate("T3", "x", Now.AddMinutes(8), Now.AddHours(12)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Phien_het_han_thi_khong_con_hoat_dong()
    {
        var s = UserSession.Start(AccountKind.Portal, Guid.NewGuid(), "T1", "Safari", "ip", true, Now, Now.AddDays(30));
        s.IsActive(Now.AddDays(31)).ShouldBeFalse();
    }

    // ---------- PortalAccount ----------

    [Fact]
    public void Tai_khoan_cong_chuan_hoa_ten_dang_nhap_va_khoa_ngung_thu_hoi_token()
    {
        PortalAccount.Create(PortalOwnerType.Student, Guid.NewGuid(), " ", null, "An").IsFailure.ShouldBeTrue();

        var p = PortalAccount.Create(PortalOwnerType.Student, Guid.NewGuid(), " HV1000 ", " An@Mail.VN ", " Nguyễn An ").Value;
        (p.Username, p.Email, p.DisplayName).ShouldBe(("hv1000", "an@mail.vn", "Nguyễn An"));
        p.CanSignIn(Now).IsSuccess.ShouldBeTrue();

        var tv = p.TokenVersion;
        p.Lock();
        p.CanSignIn(Now).Error.Code.ShouldBe("auth.account_locked");
        p.Deactivate();
        p.CanSignIn(Now).Error.Code.ShouldBe("auth.account_inactive");
        p.TokenVersion.ShouldBe(tv + 2);
        p.Activate();
        p.CanSignIn(Now).IsSuccess.ShouldBeTrue();
    }

    // ---------- ApprovalRequest ----------

    private static ApprovalRequest Request(Guid by) =>
        ApprovalRequest.Create("DY-2611-0001", ApprovalKind.FinancePermission, "Cấp quyền", null, null, "{}", by, "A", Now);

    [Fact]
    public void Nguoi_tao_khong_tu_duyet_nguoi_khac_duyet_roi_thi_khong_xu_ly_lai()
    {
        var maker = Guid.NewGuid();
        var r = Request(maker);
        r.Approve(maker, "A", null, Now).Error.ShouldBe(ApprovalErrors.SelfApproval);

        r.Approve(Guid.NewGuid(), "B", "ok", Now).IsSuccess.ShouldBeTrue();
        r.Status.ShouldBe(ApprovalStatus.Approved);
        r.DecidedByName.ShouldBe("B");
        r.Reject(Guid.NewGuid(), "C", "no", Now).Error.ShouldBe(ApprovalErrors.NotPending);
        r.Withdraw(maker, Now).Error.ShouldBe(ApprovalErrors.NotPending);
    }

    [Fact]
    public void Chi_nguoi_tao_rut_lai_duoc()
    {
        var maker = Guid.NewGuid();
        var r = Request(maker);
        r.Withdraw(Guid.NewGuid(), Now).Error.ShouldBe(ApprovalErrors.OnlyRequester);
        r.Withdraw(maker, Now).IsSuccess.ShouldBeTrue();
        r.Status.ShouldBe(ApprovalStatus.Withdrawn);
    }

    [Fact]
    public void Tu_choi_ghi_ly_do()
    {
        var r = Request(Guid.NewGuid());
        r.Reject(Guid.NewGuid(), "B", "Chưa cần", Now).IsSuccess.ShouldBeTrue();
        (r.Status, r.Note).ShouldBe((ApprovalStatus.Rejected, "Chưa cần"));
    }
}
