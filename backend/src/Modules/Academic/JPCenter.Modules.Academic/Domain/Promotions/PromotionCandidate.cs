using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;

namespace JPCenter.Modules.Academic.Domain.Promotions;

/// <summary>
/// Lên lớp: HV lớp sắp kết thúc (A) được GIỮ CHỖ TẠM vào lớp kỳ sau (B) — chưa tính sĩ số, chưa hóa đơn.
/// Lớp A kết thúc → giao Sale tư vấn NGƯỜI QUYẾT ĐỊNH (HV nhỏ → phụ huynh). Đồng ý → Students tạo ghi danh thật.
/// Hết hạn giữ chỗ chưa chốt → tự trả chỗ. Bảng: academic.promotion_candidates (+ promotion_notes)
/// </summary>
public sealed class PromotionCandidate : AggregateRoot<PromotionCandidateId>, IBranchScoped, IAuditable
{
    private readonly List<PromotionNote> _notes = [];

    private PromotionCandidate() { }

    /// <summary>Students.Student</summary>
    public Guid StudentId { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Lớp đang học (sắp kết thúc)</summary>
    public ClassId FromClassId { get; private set; }
    /// <summary>Lớp đề xuất lên (Chờ khai giảng) — đổi được khi tư vấn</summary>
    public ClassId? ToClassId { get; private set; }
    public bool SeatHeld { get; private set; }
    /// <summary>Hạn giữ chỗ (mặc định 7 ngày trước khai giảng)</summary>
    public DateOnly? HoldUntil { get; private set; }
    public PromotionStatus Status { get; private set; }
    public PromotionSource Source { get; private set; }
    /// <summary>Lúc giao cho Sale (khi lớp A kết thúc)</summary>
    public DateTimeOffset? HandedOverAt { get; private set; }
    /// <summary>Sale phụ trách (Hr.Employee)</summary>
    public Guid? SaleId { get; private set; }
    /// <summary>HV / PH bấm "Quan tâm" trên cổng</summary>
    public DateTimeOffset? InterestAt { get; private set; }
    public InterestBy? InterestBy { get; private set; }
    public string? DeclineReason { get; private set; }
    /// <summary>Ghi danh đã tạo khi chốt (Students.Enrollment)</summary>
    public Guid? EnrollmentId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public Guid? ConfirmedBy { get; private set; }
    public IReadOnlyList<PromotionNote> Notes => _notes;

    /// <summary>Hạn giữ chỗ mặc định: N ngày trước khai giảng lớp đích</summary>
    public const int DefaultHoldDaysBeforeStart = 7;

    /// <summary>Thêm HV vào danh sách lên lớp (DT-17 bước 1); trùng HV × lớp — handler kiểm</summary>
    public static Result<PromotionCandidate> Create(Guid studentId, BranchId branchId, ClassId fromClassId, PromotionSource source) =>
        new PromotionCandidate
        {
            Id = PromotionCandidateId.New(), StudentId = studentId, BranchId = branchId, FromClassId = fromClassId, Source = source,
            Status = PromotionStatus.Pending,
        };

    /// <summary>Hạn giữ chỗ mặc định = khai giảng − 7 ngày (không trước hôm nay)</summary>
    public static DateOnly DefaultHoldUntil(DateOnly targetStart, DateOnly today)
    {
        var d = targetStart.AddDays(-DefaultHoldDaysBeforeStart);
        return d < today ? today : d;
    }

    /// <summary>Chọn / đổi lớp đích mà chưa giữ chỗ</summary>
    public Result ChooseTarget(ClassId? toClassId)
    {
        if (IsClosed) return PromotionErrors.Closed;
        if (toClassId == FromClassId) return PromotionErrors.SameClass;
        ToClassId = toClassId;
        if (toClassId is null) ReleaseSeat();
        return Result.Success();
    }

    /// <summary>
    /// Giữ chỗ tạm (DT-17 bước 2) — không tính sĩ số, không hóa đơn. Không truyền lớp → dùng lớp đích đã chọn (E1: chưa có → lỗi).
    /// Vượt sĩ số chỉ cảnh báo (E2) — handler.
    /// </summary>
    public Result HoldSeat(ClassId? toClassId, DateOnly holdUntil, DateOnly today)
    {
        if (IsClosed) return PromotionErrors.Closed;
        var target = toClassId ?? ToClassId;
        if (target is null) return PromotionErrors.ToClassRequired;
        if (target == FromClassId) return PromotionErrors.SameClass;
        if (holdUntil < today) return PromotionErrors.InvalidHoldUntil;
        (ToClassId, SeatHeld, HoldUntil) = (target, true, holdUntil);
        return Result.Success();
    }

    public void ReleaseSeat() => (SeatHeld, HoldUntil) = (false, null);

    /// <summary>Job hằng ngày (DT-17.E3): quá hạn giữ chỗ mà chưa chốt → trả chỗ</summary>
    public bool ExpireHold(DateOnly today)
    {
        if (!SeatHeld || IsClosed || HoldUntil is not { } until || until >= today) return false;
        ReleaseSeat();
        return true;
    }

    /// <summary>Giao Sale (lớp cũ kết thúc, hoặc "Giao Sale ngay" — E4). Đã giao thì chỉ đổi Sale.</summary>
    public Result HandOver(Guid? saleId, DateTimeOffset at)
    {
        if (IsClosed) return PromotionErrors.Closed;
        SaleId = saleId ?? SaleId;
        HandedOverAt ??= at;
        return Result.Success();
    }

    public bool IsHandedOver => HandedOverAt is not null;

    /// <summary>HV / PH bấm "Quan tâm" lớp kỳ sau trên cổng (giữ lần bấm đầu)</summary>
    public Result MarkInterest(InterestBy by, DateTimeOffset at)
    {
        if (IsClosed) return PromotionErrors.Closed;
        if (ToClassId is null) return PromotionErrors.ToClassRequired;
        if (InterestAt is null) (InterestBy, InterestAt) = (by, at);
        return Result.Success();
    }

    public Result AddNote(string text, string? channel, Guid by, string byName, DateTimeOffset at)
    {
        if (IsClosed) return PromotionErrors.Closed;
        if (string.IsNullOrWhiteSpace(text)) return PromotionErrors.NoteRequired;
        _notes.Add(new PromotionNote(text.Trim(), channel, by, byName, at));
        if (Status == PromotionStatus.Pending) Status = PromotionStatus.Consulting;
        return Result.Success();
    }

    public Result MarkUnreachable()
    {
        if (IsClosed) return PromotionErrors.Closed;
        Status = PromotionStatus.Unreachable;
        return Result.Success();
    }

    public Result Confirm(Guid enrollmentId, Guid by, DateTimeOffset at)
    {
        if (IsClosed) return PromotionErrors.Closed;
        if (ToClassId is null) return PromotionErrors.ToClassRequired;
        (Status, EnrollmentId, ConfirmedBy, ConfirmedAt, SeatHeld) = (PromotionStatus.Confirmed, enrollmentId, by, at, false);
        return Result.Success();
    }

    public Result Decline(string reason)
    {
        if (IsClosed) return PromotionErrors.Closed;
        if (string.IsNullOrWhiteSpace(reason)) return PromotionErrors.DeclineReasonRequired;
        (Status, DeclineReason, SeatHeld, HoldUntil) = (PromotionStatus.Declined, reason.Trim(), false, null);
        return Result.Success();
    }

    public bool IsClosed => Status is PromotionStatus.Confirmed or PromotionStatus.Declined;
}
