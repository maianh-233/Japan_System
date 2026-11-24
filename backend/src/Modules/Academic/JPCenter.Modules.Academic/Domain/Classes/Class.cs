using System.Text.RegularExpressions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes.Events;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Rooms;

namespace JPCenter.Modules.Academic.Domain.Classes;

/// <summary>
/// Lớp học — mở theo 1 khóa, chốt 1 lộ trình lúc mở (lộ trình ra phiên bản mới không ảnh hưởng lớp đã mở).
/// Vòng đời: Enrolling → Ongoing ⇄ Paused → Finished; Cancelled từ trạng thái chưa kết thúc.
/// Sửa: Enrolling sửa hết · Ongoing / Paused khóa khóa học, lộ trình, mã, chi nhánh · Finished / Cancelled chỉ ghi chú.
/// Bảng: academic.classes. User story 07 — DT-02 → DT-07.
/// </summary>
public sealed partial class Class : AggregateRoot<ClassId>, IBranchScoped, IAuditable, ISoftDeletable
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
    /// <summary>Học phí / HV (lấy từ khóa, chỉnh được khi mở lớp). Đổi sau khi có ghi danh chỉ áp cho ghi danh mới.</summary>
    public Money Fee { get; private set; }
    public ClassStatus Status { get; private set; }
    /// <summary>Lớp trước (lên lớp): lớp này mở để đón học viên từ lớp đó</summary>
    public ClassId? PreviousClassId { get; private set; }
    /// <summary>Lý do tạm dừng (DT-06) — xóa khi học lại</summary>
    public string? PauseReason { get; private set; }
    public string? CancelReason { get; private set; }
    public string? Note { get; private set; }

    /// <summary>Số buổi = tổng giờ ÷ số giờ mỗi buổi (làm tròn lên)</summary>
    public int SessionCount => SessionsFor(TotalHours, StartTime, EndTime);

    public bool IsClosed => Status is ClassStatus.Finished or ClassStatus.Cancelled;

    /// <summary>Lớp đã khai giảng (đang học / tạm dừng) — khóa đổi khóa học, lộ trình, mã, chi nhánh</summary>
    public bool IsRunning => Status is ClassStatus.Ongoing or ClassStatus.Paused;

    public static int SessionsFor(decimal totalHours, TimeOnly start, TimeOnly end)
    {
        var perSession = (decimal)(end - start).TotalHours;
        return perSession <= 0 ? 0 : (int)Math.Ceiling(totalHours / perSession);
    }

    /// <summary>Tiền tố mã lớp theo loại GV của khóa: GVV_ · GVN_ · GVVN_</summary>
    public static string CodePrefix(TeacherType type) => type switch
    {
        TeacherType.Jp => "GVN_",
        TeacherType.Mixed => "GVVN_",
        _ => "GVV_",
    };

    /// <summary>Chuẩn hóa mã: bỏ khoảng trắng, chữ hoa; phải đúng tiền tố + đuôi gồm A-Z, 0-9, "-"</summary>
    public static Result<string> NormalizeCode(string code, TeacherType type)
    {
        var prefix = CodePrefix(type);
        var value = (code ?? "").Trim().ToUpperInvariant();
        if (!value.StartsWith(prefix, StringComparison.Ordinal) || !SuffixPattern().IsMatch(value[prefix.Length..]))
            return ClassErrors.InvalidCode(prefix);
        return value;
    }

    /// <param name="teacherType">Loại GV của khóa — quyết định tiền tố mã</param>
    /// <param name="roomCapacity">Sức chứa phòng đã chọn (null = online / không kiểm)</param>
    public static Result<Class> Create(CourseId courseId, CurriculumId? curriculumId, TeacherType teacherType, ClassSetup setup,
        int? roomCapacity)
    {
        var check = Validate(setup, teacherType, roomCapacity, enrolled: 0);
        if (check.IsFailure) return check.Error;
        var c = new Class { Id = ClassId.New(), CourseId = courseId, CurriculumId = curriculumId, Status = ClassStatus.Enrolling };
        c.Apply(check.Value, setup);
        return c;
    }

    /// <summary>
    /// Sửa lớp (DT-07). <paramref name="enrolled"/> = sĩ số hiện tại (không cho max nhỏ hơn).
    /// Lớp đã đóng: chỉ đổi được ghi chú — đổi trường khác báo <see cref="ClassErrors.Closed"/>.
    /// </summary>
    public Result Update(CourseId courseId, CurriculumId? curriculumId, TeacherType teacherType, ClassSetup setup, int? roomCapacity,
        int enrolled)
    {
        if (IsClosed)
        {
            if (!SameExceptNote(courseId, curriculumId, setup)) return ClassErrors.Closed;
            Note = Clean(setup.Note);
            return Result.Success();
        }

        var check = Validate(setup, teacherType, roomCapacity, enrolled);
        if (check.IsFailure) return check.Error;
        if (IsRunning && (courseId != CourseId || curriculumId != CurriculumId || check.Value != Code || setup.BranchId != BranchId))
            return ClassErrors.Locked;

        (CourseId, CurriculumId) = (courseId, curriculumId);
        Apply(check.Value, setup);
        return Result.Success();
    }

    /// <summary>Lịch tuần / giờ / GV / phòng khác bản đang lưu — để hỏi "Áp cho các buổi chưa dạy?" (DT-07.E4)</summary>
    public bool ScheduleDiffers(ClassSetup s) =>
        !s.Weekdays.Order().SequenceEqual(Weekdays.Order()) || s.StartTime != StartTime || s.EndTime != EndTime
        || !s.TeacherIds.Order().SequenceEqual(TeacherIds.Order()) || !s.AssistantIds.Order().SequenceEqual(AssistantIds.Order())
        || s.RoomId != RoomId || s.Online != Online;

    /// <summary>Sau khi sinh lịch (DT-08): ngày kết thúc dự kiến = buổi cuối</summary>
    public void SetPlannedEnd(DateOnly date)
    {
        if (date >= PlannedStartDate) PlannedEndDate = date;
    }

    /// <summary>
    /// Khai giảng (DT-03). <paramref name="enrolled"/> null = chưa biết sĩ số (module Học viên chưa nối) → không chặn.
    /// Sĩ số dưới tối thiểu chỉ cảnh báo (handler trả warnings) — 0 HV mới chặn.
    /// </summary>
    public Result Start(DateOnly actualStart, int? enrolled)
    {
        if (Status != ClassStatus.Enrolling) return ClassErrors.NotEnrolling;
        if (enrolled == 0) return ClassErrors.NotEnoughStudents;
        (Status, ActualStartDate) = (ClassStatus.Ongoing, actualStart);
        Raise(new ClassStarted(Id, actualStart));
        return Result.Success();
    }

    /// <summary>Tạm dừng (DT-06) — buổi chưa dạy giữ nguyên trên lịch</summary>
    public Result Pause(string reason)
    {
        if (Status != ClassStatus.Ongoing) return ClassErrors.NotOngoing;
        if (string.IsNullOrWhiteSpace(reason)) return ClassErrors.PauseReason;
        (Status, PauseReason) = (ClassStatus.Paused, reason.Trim());
        return Result.Success();
    }

    public Result Resume()
    {
        if (Status != ClassStatus.Paused) return ClassErrors.NotPaused;
        (Status, PauseReason) = (ClassStatus.Ongoing, null);
        return Result.Success();
    }

    /// <summary>Kết thúc lớp (DT-04) — handler hủy các buổi chưa dạy ("Lớp kết thúc sớm")</summary>
    public Result Finish(DateOnly actualEnd)
    {
        if (!IsRunning) return ClassErrors.NotOngoing;
        if (ActualStartDate is { } start && actualEnd < start) return ClassErrors.InvalidDates;
        (Status, ActualEndDate, PauseReason) = (ClassStatus.Finished, actualEnd, null);
        Raise(new ClassFinished(Id, actualEnd));
        return Result.Success();
    }

    /// <summary>Hủy lớp (DT-05) — không hoàn tác; handler hủy các buổi chưa dạy</summary>
    public Result Cancel(string reason)
    {
        if (IsClosed) return ClassErrors.Closed;
        if (string.IsNullOrWhiteSpace(reason)) return ClassErrors.CancelReason;
        (Status, CancelReason, PauseReason) = (ClassStatus.Cancelled, reason.Trim(), null);
        Raise(new ClassCancelled(Id, CancelReason));
        return Result.Success();
    }

    /// <summary>Ghi danh thêm (Students gọi qua Contracts): lớp đóng / tạm dừng → closed; đủ max → full</summary>
    public Result CanEnroll(int enrolled)
    {
        if (IsClosed || Status == ClassStatus.Paused) return ClassErrors.Closed;
        return enrolled < MaxStudents ? Result.Success() : ClassErrors.Full;
    }

    private static Result<string> Validate(ClassSetup s, TeacherType teacherType, int? roomCapacity, int enrolled)
    {
        var code = NormalizeCode(s.Code, teacherType);
        if (code.IsFailure) return code.Error;
        if (string.IsNullOrWhiteSpace(s.Name)) return ClassErrors.NameRequired;
        if (s.TeacherIds.Distinct().Count() is < 1 or > 2) return ClassErrors.TeacherCount;
        if (teacherType == TeacherType.Mixed && s.TeacherIds.Distinct().Count() != 2) return ClassErrors.MixedTeachers;
        if (s.EndTime <= s.StartTime) return ClassErrors.InvalidTime;
        if (s.Weekdays.Count == 0 || s.Weekdays.Any(d => d is < 1 or > 7)) return ClassErrors.InvalidWeekdays;
        if (s.TotalHours <= 0) return ClassErrors.InvalidHours;
        if (s.MinStudents < 1 || s.MaxStudents < s.MinStudents) return ClassErrors.InvalidSize;
        if (roomCapacity is { } cap && s.MaxStudents > cap) return ClassErrors.RoomTooSmall(cap);
        if (s.MaxStudents < enrolled) return ClassErrors.BelowEnrolled(enrolled);
        if (s.PlannedEndDate < s.PlannedStartDate) return ClassErrors.InvalidDates;
        if (s.Fee.Amount < 0) return ClassErrors.InvalidFee;
        if (!s.Online && s.RoomId is null) return ClassErrors.RoomRequired;
        return code.Value;
    }

    private void Apply(string code, ClassSetup s)
    {
        Code = code;
        Name = s.Name.Trim();
        BranchId = s.BranchId;
        TeacherIds = [.. s.TeacherIds.Distinct()];
        AssistantIds = [.. s.AssistantIds.Distinct().Except(TeacherIds)];
        Online = s.Online;
        RoomId = s.Online ? null : s.RoomId;
        // Lớp online để trống link → hệ thống tự tạo phòng (TH-08) hoặc hiện "Chưa có link phòng"
        MeetingUrl = s.Online ? Clean(s.MeetingUrl) : null;
        (PlannedStartDate, PlannedEndDate) = (s.PlannedStartDate, s.PlannedEndDate);
        Weekdays = [.. s.Weekdays.Distinct().Order()];
        (StartTime, EndTime, TotalHours) = (s.StartTime, s.EndTime, s.TotalHours);
        (MinStudents, MaxStudents, Fee) = (s.MinStudents, s.MaxStudents, s.Fee);
        PreviousClassId = s.PreviousClassId;
        Note = Clean(s.Note);
    }

    /// <summary>
    /// TH-08 — gắn link phòng học online hệ thống vừa tạo (Zoom / Meet). Chỉ lớp online chưa có link: link giáo vụ nhập tay được giữ (E1).
    /// </summary>
    public Result SetMeetingRoom(string url)
    {
        if (!Online) return ClassErrors.NotOnline;
        if (MeetingUrl is not null) return ClassErrors.HasMeetingUrl;
        if (string.IsNullOrWhiteSpace(url)) return ClassErrors.MeetingRoomFailed;
        MeetingUrl = url.Trim();
        return Result.Success();
    }

    /// <summary>Lớp online chưa có link phòng — cần tạo phòng tự động / nhập tay</summary>
    public bool NeedsMeetingRoom => Online && MeetingUrl is null && Status is not (ClassStatus.Finished or ClassStatus.Cancelled);

    private bool SameExceptNote(CourseId courseId, CurriculumId? curriculumId, ClassSetup s) =>
        courseId == CourseId && curriculumId == CurriculumId && s.Code.Trim().ToUpperInvariant() == Code && s.Name.Trim() == Name
        && s.BranchId == BranchId && !ScheduleDiffers(s) && (!s.Online || Clean(s.MeetingUrl) == MeetingUrl)
        && s.PlannedStartDate == PlannedStartDate && s.PlannedEndDate == PlannedEndDate && s.TotalHours == TotalHours
        && s.MinStudents == MinStudents && s.MaxStudents == MaxStudents && s.Fee == Fee && s.PreviousClassId == PreviousClassId;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [GeneratedRegex("^[A-Z0-9-]+$")]
    private static partial Regex SuffixPattern();
}
