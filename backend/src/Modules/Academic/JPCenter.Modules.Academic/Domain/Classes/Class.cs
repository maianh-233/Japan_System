using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes.Events;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Rooms;

namespace JPCenter.Modules.Academic.Domain.Classes;

/// <summary>
/// Lớp học — mở theo 1 khóa, chốt 1 lộ trình lúc mở. Vòng đời: Enrolling → Ongoing ⇄ Paused → Finished; Cancelled từ trạng thái
/// chưa kết thúc (chỉ trung tâm). Chỉ khi Enrolling mới sửa toàn bộ thông tin. Bảng: academic.classes
/// </summary>
public sealed class Class : AggregateRoot<ClassId>, IBranchScoped, IAuditable, ISoftDeletable
{
    private Class() { }

    public CourseId CourseId { get; private set; }
    public CurriculumId? CurriculumId { get; private set; }
    /// <summary>Mã đầy đủ, vd GVV_N3-K12 (tiền tố theo loại GV của khóa)</summary>
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public BranchId BranchId { get; private set; }
    /// <summary>1 GV (2 với lớp Việt + Nhật) — Hr.Employee</summary>
    public List<Guid> TeacherIds { get; private set; } = [];
    /// <summary>Trợ giảng — Hr.Employee</summary>
    public List<Guid> AssistantIds { get; private set; } = [];
    public RoomId? RoomId { get; private set; }
    public bool Online { get; private set; }
    public string? MeetingUrl { get; private set; }
    public DateOnly PlannedStartDate { get; private set; }
    public DateOnly PlannedEndDate { get; private set; }
    public DateOnly? ActualStartDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }
    /// <summary>Thứ học: 1 = Thứ 2 … 7 = Chủ nhật</summary>
    public List<int> Weekdays { get; private set; } = [];
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public decimal TotalHours { get; private set; }
    public int MinStudents { get; private set; }
    public int MaxStudents { get; private set; }
    /// <summary>Học phí / HV (lấy từ khóa, chỉnh được khi mở lớp)</summary>
    public Money Fee { get; private set; }
    public ClassStatus Status { get; private set; }
    /// <summary>Lớp trước (lên lớp): lớp này mở để đón học viên từ lớp đó</summary>
    public ClassId? PreviousClassId { get; private set; }
    public string? CancelReason { get; private set; }
    public string? Note { get; private set; }

    /// <summary>Số buổi = tổng giờ ÷ số giờ mỗi buổi</summary>
    public int SessionCount => (int)Math.Ceiling(TotalHours / (decimal)(EndTime - StartTime).TotalHours);

    public static Result<Class> Create(CourseId courseId, CurriculumId? curriculumId, string code, string name, BranchId branchId,
        List<Guid> teacherIds, List<Guid> assistantIds, RoomId? roomId, bool online, string? meetingUrl, DateOnly plannedStart,
        DateOnly plannedEnd, List<int> weekdays, TimeOnly startTime, TimeOnly endTime, decimal totalHours, int minStudents,
        int maxStudents, Money fee, ClassId? previousClassId, string? note)
    {
        if (teacherIds.Count is < 1 or > 2) return ClassErrors.TeacherCount;
        if (endTime <= startTime) return ClassErrors.InvalidTime;
        if (weekdays.Count == 0 || weekdays.Any(d => d is < 1 or > 7)) return ClassErrors.InvalidWeekdays;
        if (minStudents < 1 || maxStudents < minStudents) return ClassErrors.InvalidSize;
        if (plannedEnd < plannedStart) return ClassErrors.InvalidDates;
        if (!online && roomId is null) return Error.Validation("academic.class.room_required", "Lớp offline cần chọn phòng");
        return new Class
        {
            Id = ClassId.New(), CourseId = courseId, CurriculumId = curriculumId, Code = code, Name = name, BranchId = branchId,
            TeacherIds = teacherIds, AssistantIds = assistantIds, RoomId = roomId, Online = online, MeetingUrl = meetingUrl,
            PlannedStartDate = plannedStart, PlannedEndDate = plannedEnd, Weekdays = weekdays, StartTime = startTime, EndTime = endTime,
            TotalHours = totalHours, MinStudents = minStudents, MaxStudents = maxStudents, Fee = fee, PreviousClassId = previousClassId,
            Note = note, Status = ClassStatus.Enrolling,
        };
    }

    /// <summary>Đổi lịch / GV / phòng — chỉ khi Chờ khai giảng (đang học thì đổi theo từng buổi)</summary>
    public Result Reschedule(DateOnly plannedStart, DateOnly plannedEnd, List<int> weekdays, TimeOnly startTime, TimeOnly endTime)
    {
        if (Status != ClassStatus.Enrolling) return ClassErrors.NotEnrolling;
        if (endTime <= startTime) return ClassErrors.InvalidTime;
        (PlannedStartDate, PlannedEndDate, Weekdays, StartTime, EndTime) = (plannedStart, plannedEnd, weekdays, startTime, endTime);
        return Result.Success();
    }

    public Result ChangeStaff(List<Guid> teacherIds, List<Guid> assistantIds)
    {
        if (teacherIds.Count is < 1 or > 2) return ClassErrors.TeacherCount;
        if (Status is ClassStatus.Finished or ClassStatus.Cancelled) return ClassErrors.Closed;
        (TeacherIds, AssistantIds) = (teacherIds, assistantIds);
        return Result.Success();
    }

    public void ChangeRoom(RoomId? roomId, bool online, string? meetingUrl) => (RoomId, Online, MeetingUrl) = (roomId, online, meetingUrl);

    /// <param name="enrolledCount">Sĩ số chính thức hiện tại</param>
    public Result Start(DateOnly actualStart, int enrolledCount)
    {
        if (Status != ClassStatus.Enrolling) return ClassErrors.NotEnrolling;
        if (enrolledCount < MinStudents) return Error.Conflict("academic.class.not_enough_students", "Chưa đủ sĩ số tối thiểu để khai giảng");
        (Status, ActualStartDate) = (ClassStatus.Ongoing, actualStart);
        Raise(new ClassStarted(Id, actualStart));
        return Result.Success();
    }

    public Result Pause()
    {
        if (Status != ClassStatus.Ongoing) return ClassErrors.NotOngoing;
        Status = ClassStatus.Paused;
        return Result.Success();
    }

    public Result Resume()
    {
        if (Status != ClassStatus.Paused) return Error.Conflict("academic.class.not_paused", "Lớp không ở trạng thái tạm dừng");
        Status = ClassStatus.Ongoing;
        return Result.Success();
    }

    public Result Finish(DateOnly actualEnd)
    {
        if (Status is not (ClassStatus.Ongoing or ClassStatus.Paused)) return ClassErrors.NotOngoing;
        (Status, ActualEndDate) = (ClassStatus.Finished, actualEnd);
        Raise(new ClassFinished(Id, actualEnd));
        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status is ClassStatus.Finished or ClassStatus.Cancelled) return ClassErrors.Closed;
        if (string.IsNullOrWhiteSpace(reason)) return Error.Validation("academic.class.cancel_reason", "Cần ghi lý do hủy lớp");
        (Status, CancelReason) = (ClassStatus.Cancelled, reason);
        Raise(new ClassCancelled(Id, reason));
        return Result.Success();
    }

    public Result CheckCapacity(int enrolledCount) =>
        enrolledCount < MaxStudents ? Result.Success() : Error.Conflict("academic.class.full", "Lớp đã đủ sĩ số tối đa");
}
