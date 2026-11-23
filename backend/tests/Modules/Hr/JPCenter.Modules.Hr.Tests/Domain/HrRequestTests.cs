using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Tests.Domain;

public sealed class HrRequestTests
{
    private static readonly DateOnly Today = new(2026, 6, 10); // thứ 4
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 3, 0, 0, TimeSpan.Zero);
    private static readonly EmployeeId Me = EmployeeId.New();
    private static readonly ApproverRef Manager = new(Guid.NewGuid(), "Quản lý");
    private static readonly ApproverRef Deputy = new(Guid.NewGuid(), "Phó GĐ");
    private static readonly ApproverRef Chief = new(Guid.NewGuid(), "KTT");
    private static readonly ApproverRef Director = new(Guid.NewGuid(), "GĐ");
    private static readonly ApproverHolders Holders = new(Deputy, Chief, Director);

    private static Result<HrRequest> Create(HrRequestType type, DateOnly from, DateOnly to, HrRequestDetails? details = null,
        IReadOnlyList<PlannedStep>? steps = null) =>
        HrRequest.Create("DT-1", type, Me, BranchId.New(), from, to, "Việc gia đình", details ?? new HrRequestDetails(),
            steps ?? [new PlannedStep("Quản lý trực tiếp", Manager)], Now, Today);

    [Fact]
    public void DN01_cap_duyet_theo_loai_don()
    {
        ApprovalChain.Build(HrRequestType.Leave, Me.Value, Manager, Holders, 2, null).Select(s => s.Approver).ShouldBe([Manager]);
        ApprovalChain.Build(HrRequestType.Leave, Me.Value, Manager, Holders, 4, null).Select(s => s.Approver).ShouldBe([Manager, Deputy]);
        ApprovalChain.Build(HrRequestType.Advance, Me.Value, Manager, Holders, 0, Money.FromTrusted(1)).Select(s => s.Approver).ShouldBe([Manager, Chief]);
        ApprovalChain.Build(HrRequestType.Proposal, Me.Value, Manager, Holders, 0, Money.FromTrusted(5_000_000)).Count.ShouldBe(1);
        ApprovalChain.Build(HrRequestType.Proposal, Me.Value, Manager, Holders, 0, Money.FromTrusted(6_000_000)).Select(s => s.Approver)
            .ShouldBe([Manager, Chief, Director]);
    }

    [Fact]
    public void DN01_nguoi_gui_la_nguoi_duyet_thanh_buoc_mo_va_gop_buoc_trung()
    {
        // Không có quản lý → bước mở
        ApprovalChain.Build(HrRequestType.Leave, Me.Value, null, Holders, 1, null).Single().Approver.ShouldBeNull();
        // Phó GĐ xin nghỉ dài: bước Phó GĐ là chính mình → mở
        var deputyHimself = ApprovalChain.Build(HrRequestType.Leave, Deputy.Id, Director, Holders, 5, null);
        deputyHimself.Select(s => s.Approver).ShouldBe([Director, null]);
        // Quản lý trực tiếp cũng là Kế toán trưởng → gộp
        ApprovalChain.Build(HrRequestType.Advance, Me.Value, Chief, Holders, 0, Money.FromTrusted(1)).Count.ShouldBe(1);
    }

    [Fact]
    public void DN01_kiem_tra_theo_loai()
    {
        Create(HrRequestType.Leave, Today, Today.AddDays(-1)).Error.Code.ShouldBe("hr.request.invalid_dates");
        var sunday = new DateOnly(2026, 6, 14);
        Create(HrRequestType.Leave, sunday, sunday).Error.Code.ShouldBe("hr.request.no_workdays");
        var leave = Create(HrRequestType.Leave, Today, sunday).Value; // T4 → CN = 4 ngày làm việc
        (leave.Days, leave.PaidLeave, leave.DeductsLeave).ShouldBe((4m, true, true));
        Create(HrRequestType.LateEarly, Today, Today.AddDays(1)).Error.Code.ShouldBe("hr.request.invalid_dates");
        Create(HrRequestType.Advance, Today, Today).Error.Code.ShouldBe("hr.request.amount_required");
        Create(HrRequestType.Proposal, Today, Today, new HrRequestDetails(Amount: Money.FromTrusted(1))).Error.Code.ShouldBe("hr.request.title_required");
        Create(HrRequestType.ShiftSwap, Today, Today, new HrRequestDetails(SwapMyShiftId: Guid.NewGuid(), SwapWithEmployeeId: Me, SwapTheirShiftId: Guid.NewGuid()))
            .Error.Code.ShouldBe("hr.request.invalid_swap");
        Create(HrRequestType.Leave, Today.AddDays(-31), Today.AddDays(-31)).Error.Code.ShouldBe("hr.request.too_old");
        Create(HrRequestType.Leave, Today.AddDays(-30), Today.AddDays(-30)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void DN02_duyet_lan_luot_va_duyet_thay()
    {
        var r = Create(HrRequestType.Leave, Today, Today.AddDays(5), steps: ApprovalChain.Build(HrRequestType.Leave, Me.Value, Manager, Holders, 5, null)).Value;
        r.Decide(Deputy.Id, "Phó GĐ", false, ApprovalDecision.Approved, null, Now).Error.Code.ShouldBe("hr.request.not_your_turn");
        r.Decide(Me.Value, "Tôi", true, ApprovalDecision.Approved, null, Now).Error.Code.ShouldBe("hr.request.self_approve");
        r.Decide(Manager.Id, "Quản lý", false, ApprovalDecision.Approved, "ok", Now).IsSuccess.ShouldBeTrue();
        r.Status.ShouldBe(HrRequestStatus.Pending);
        r.CurrentStep!.ApproverId.ShouldBe(Deputy.Id);

        var hr = Guid.NewGuid();
        r.CanDecide(hr, canOverride: false).ShouldBeFalse();
        r.CanDecide(hr, canOverride: true).ShouldBeTrue();
        r.Decide(hr, "Nhân sự", true, ApprovalDecision.Approved, null, Now).IsSuccess.ShouldBeTrue();
        r.Status.ShouldBe(HrRequestStatus.Approved);
        r.Steps[1].OnBehalf.ShouldBeTrue();
        r.Steps[1].DecidedByName.ShouldBe("Nhân sự");
        r.Decide(Manager.Id, "Quản lý", true, ApprovalDecision.Approved, null, Now).Error.Code.ShouldBe("hr.request.not_pending");
    }

    [Fact]
    public void DN02_tu_choi_can_ly_do_va_dung_ngay()
    {
        var r = Create(HrRequestType.Leave, Today, Today.AddDays(5), steps: ApprovalChain.Build(HrRequestType.Leave, Me.Value, Manager, Holders, 5, null)).Value;
        r.Decide(Manager.Id, "Quản lý", false, ApprovalDecision.Rejected, " ", Now).Error.Code.ShouldBe("hr.request.reason_required");
        r.Decide(Manager.Id, "Quản lý", false, ApprovalDecision.Rejected, "Cao điểm", Now).IsSuccess.ShouldBeTrue();
        r.Status.ShouldBe(HrRequestStatus.Rejected);
        r.Steps[1].Decision.ShouldBeNull();
    }

    [Fact]
    public void DN02_buoc_mo_chi_nguoi_co_quyen_duyet_thay()
    {
        var r = Create(HrRequestType.Leave, Today, Today, steps: [new PlannedStep("Quản lý trực tiếp", null)]).Value;
        r.Decide(Manager.Id, "Quản lý", false, ApprovalDecision.Approved, null, Now).Error.Code.ShouldBe("hr.request.not_your_turn");
        r.Decide(Manager.Id, "Quản lý", true, ApprovalDecision.Approved, null, Now).IsSuccess.ShouldBeTrue();
        r.Steps[0].OnBehalf.ShouldBeFalse();
    }

    [Fact]
    public void DN03_chi_nguoi_gui_huy_khi_con_cho()
    {
        var r = Create(HrRequestType.LateEarly, Today, Today).Value;
        r.Cancel(EmployeeId.New()).Error.Code.ShouldBe("hr.request.only_owner");
        r.Cancel(Me).IsSuccess.ShouldBeTrue();
        r.Cancel(Me).Error.Code.ShouldBe("hr.request.not_pending");
        r.CurrentStep.ShouldBeNull();
    }
}
