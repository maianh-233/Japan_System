using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>Tin tuyển dụng (TD-01): Đang tuyển ↔ Tạm dừng → Đã đóng (mở lại được). Bảng: hr.jobs</summary>
public sealed class Job : AggregateRoot<JobId>, IBranchScoped, IAuditable, ISoftDeletable
{
    private Job() { }

    public string Title { get; private set; } = null!;
    public BranchId BranchId { get; private set; }
    public Department Department { get; private set; }
    public EmployeeKind Kind { get; private set; }
    public int Quantity { get; private set; }
    /// <summary>Khoảng lương hiển thị, vd "8–12 triệu"</summary>
    public string? SalaryRange { get; private set; }
    public JobStatus Status { get; private set; }
    public DateOnly PostedAt { get; private set; }
    public DateOnly Deadline { get; private set; }
    /// <summary>Kênh đăng: TopCV, Facebook...</summary>
    public List<string> Channels { get; private set; } = [];
    public string? Description { get; private set; }

    public bool IsOpen => Status == JobStatus.Open;

    public static Result<Job> Create(string title, BranchId branchId, Department department, EmployeeKind kind, int quantity, string? salaryRange,
        DateOnly postedAt, DateOnly deadline, List<string> channels, string? description)
    {
        var job = new Job { Id = JobId.New(), PostedAt = postedAt, Status = JobStatus.Open };
        var set = job.Update(title, branchId, department, kind, quantity, salaryRange, deadline, channels, description);
        return set.IsFailure ? set.Error : job;
    }

    public Result Update(string title, BranchId branchId, Department department, EmployeeKind kind, int quantity, string? salaryRange,
        DateOnly deadline, List<string> channels, string? description)
    {
        if (string.IsNullOrWhiteSpace(title)) return RecruitmentErrors.TitleRequired;
        if (quantity <= 0) return RecruitmentErrors.InvalidQuantity;
        if (deadline < PostedAt) return RecruitmentErrors.InvalidDeadline;
        (Title, BranchId, Department, Kind, Quantity, SalaryRange, Deadline, Description) =
            (title.Trim(), branchId, department, kind, quantity, Clean(salaryRange), deadline, Clean(description));
        Channels = [.. channels.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct()];
        return Result.Success();
    }

    public Result Pause()
    {
        if (Status != JobStatus.Open) return RecruitmentErrors.JobStatus;
        Status = JobStatus.Paused;
        return Result.Success();
    }

    public Result Reopen()
    {
        if (Status == JobStatus.Open) return RecruitmentErrors.JobStatus;
        Status = JobStatus.Open;
        return Result.Success();
    }

    public Result Close()
    {
        if (Status == JobStatus.Closed) return RecruitmentErrors.JobStatus;
        Status = JobStatus.Closed;
        return Result.Success();
    }

    /// <summary>TD-04 — số người nhận việc đủ số lượng → tự đóng tin. Trả true nếu vừa đóng.</summary>
    public bool CloseIfFilled(int hired)
    {
        if (hired < Quantity || Status == JobStatus.Closed) return false;
        Status = JobStatus.Closed;
        return true;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
