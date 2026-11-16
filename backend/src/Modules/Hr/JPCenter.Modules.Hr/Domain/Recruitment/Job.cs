using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>Tin tuyển dụng. Bảng: hr.jobs</summary>
public sealed class Job : AggregateRoot<JobId>, IBranchScoped, IAuditable, ISoftDeletable
{
    private Job() { }

    public string Title { get; private set; } = null!;
    public BranchId BranchId { get; private set; }
    public Employees.Department Department { get; private set; }
    public Employees.EmployeeKind Kind { get; private set; }
    public int Quantity { get; private set; }
    /// <summary>Khoảng lương hiển thị, vd "8–12 triệu"</summary>
    public string? SalaryRange { get; private set; }
    public JobStatus Status { get; private set; }
    public DateOnly PostedAt { get; private set; }
    public DateOnly Deadline { get; private set; }
    /// <summary>Kênh đăng: TopCV, Facebook...</summary>
    public List<string> Channels { get; private set; } = [];
    public string? Description { get; private set; }

    public static Result<Job> Create(string title, BranchId branchId, Employees.Department department, Employees.EmployeeKind kind, int quantity,
        string? salaryRange, DateOnly postedAt, DateOnly deadline, List<string> channels, string? description)
    {
        if (quantity <= 0) return Error.Validation("hr.job.invalid_quantity", "Số lượng phải lớn hơn 0");
        if (deadline < postedAt) return Error.Validation("hr.job.invalid_deadline", "Hạn nộp phải sau ngày đăng");
        return new Job
        {
            Id = JobId.New(), Title = title, BranchId = branchId, Department = department, Kind = kind, Quantity = quantity, SalaryRange = salaryRange,
            PostedAt = postedAt, Deadline = deadline, Channels = channels, Description = description, Status = JobStatus.Open,
        };
    }

    public void Pause() => Status = JobStatus.Paused;
    public void Reopen() => Status = JobStatus.Open;
    public void Close() => Status = JobStatus.Closed;
}
