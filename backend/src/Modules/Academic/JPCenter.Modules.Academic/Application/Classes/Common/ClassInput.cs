using FluentValidation;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Classes.Common;

/// <summary>
/// Form Mở lớp / Sửa lớp (DT-02, DT-07). <paramref name="PlannedEndDate"/> bỏ trống = tự tính theo lịch tuần (bỏ ngày lễ).
/// Quy tắc nghiệp vụ (mã, số GV, giờ, sĩ số…) nằm trong <see cref="Class"/>; ở đây chỉ kiểm độ dài / định dạng.
/// </summary>
public sealed record ClassInput(
    Guid CourseId,
    Guid? CurriculumId,
    string Code,
    string Name,
    Guid BranchId,
    List<Guid> TeacherIds,
    List<Guid>? AssistantIds,
    Guid? RoomId,
    bool Online,
    string? MeetingUrl,
    DateOnly PlannedStartDate,
    DateOnly? PlannedEndDate,
    List<int> Weekdays,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal TotalHours,
    int MinStudents,
    int MaxStudents,
    decimal Fee,
    Guid? PreviousClassId,
    string? Note)
{
    public ClassSetup ToSetup(DateOnly plannedEnd) => new(Code, Name, new BranchId(BranchId), TeacherIds ?? [], AssistantIds ?? [],
        Online || RoomId is null ? null : new RoomId(RoomId.Value), Online, MeetingUrl, PlannedStartDate, plannedEnd, Weekdays ?? [],
        StartTime, EndTime, TotalHours, MinStudents, MaxStudents, Money.FromTrusted(Fee),
        PreviousClassId is { } p ? new ClassId(p) : null, Note);
}

public sealed class ClassInputValidator : AbstractValidator<ClassInput>
{
    public ClassInputValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty().WithMessage("Chọn khóa học");
        RuleFor(x => x.BranchId).NotEmpty().WithMessage("Chọn chi nhánh");
        RuleFor(x => x.Code).NotEmpty().WithMessage("Nhập mã lớp").MaximumLength(40);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nhập tên lớp").MaximumLength(200);
        RuleFor(x => x.MeetingUrl).MaximumLength(500);
        RuleFor(x => x.TotalHours).InclusiveBetween(0.5m, 2000m);
        RuleFor(x => x.MaxStudents).InclusiveBetween(1, 500);
        RuleFor(x => x.Fee).GreaterThanOrEqualTo(0).Must(f => decimal.Truncate(f) == f).WithMessage("Học phí VND không có số lẻ");
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}
