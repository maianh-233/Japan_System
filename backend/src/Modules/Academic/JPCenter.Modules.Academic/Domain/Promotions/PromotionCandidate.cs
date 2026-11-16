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

    public static PromotionCandidate Create(Guid studentId, BranchId branchId, ClassId fromClassId, ClassId? toClassId, PromotionSource source,
        DateOnly? holdUntil) =>
        new()
        {
            Id = PromotionCandidateId.New(), StudentId = studentId, BranchId = branchId, FromClassId = fromClassId, ToClassId = toClassId,
            Source = source, SeatHeld = toClassId is not null, HoldUntil = toClassId is null ? null : holdUntil, Status = PromotionStatus.Pending,
        };

    public Result HoldSeat(ClassId toClassId, DateOnly holdUntil)
    {
        if (IsClosed) return PromotionErrors.Closed;
        (ToClassId, SeatHeld, HoldUntil) = (toClassId, true, holdUntil);
        return Result.Success();
    }

    public void ReleaseSeat() => (SeatHeld, HoldUntil) = (false, null);

    public void HandOver(Guid? saleId, DateTimeOffset at) => (SaleId, HandedOverAt) = (saleId, at);

    public void MarkInterest(InterestBy by, DateTimeOffset at) => (InterestBy, InterestAt) = (by, at);

    public Result AddNote(string text, string? channel, Guid by, string byName, DateTimeOffset at)
    {
        if (IsClosed) return PromotionErrors.Closed;
        _notes.Add(new PromotionNote(text, channel, by, byName, at));
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
        if (ToClassId is null) return Error.Validation("academic.promotion.to_class_required", "Chưa chọn lớp lên");
        (Status, EnrollmentId, ConfirmedBy, ConfirmedAt, SeatHeld) = (PromotionStatus.Confirmed, enrollmentId, by, at, false);
        return Result.Success();
    }

    public Result Decline(string reason)
    {
        if (IsClosed) return PromotionErrors.Closed;
        (Status, DeclineReason, SeatHeld, HoldUntil) = (PromotionStatus.Declined, reason, false, null);
        return Result.Success();
    }

    private bool IsClosed => Status is PromotionStatus.Confirmed or PromotionStatus.Declined;
}
