using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>
/// Nhận việc: checklist giấy tờ / tài khoản / thiết bị / đào tạo; xong → tạo hồ sơ nhân viên + tài khoản.
/// Bảng: hr.onboardings (+ onboarding_items)
/// </summary>
public sealed class Onboarding : AggregateRoot<OnboardingId>, IBranchScoped, IAuditable
{
    private readonly List<OnboardingItem> _items = [];

    private Onboarding() { }

    public CandidateId? CandidateId { get; private set; }
    public string FullName { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string Position { get; private set; } = null!;
    public Department Department { get; private set; }
    public EmployeeKind Kind { get; private set; }
    public BranchId BranchId { get; private set; }
    public DateOnly StartDate { get; private set; }
    /// <summary>Người hướng dẫn (Hr.Employee)</summary>
    public EmployeeId MentorId { get; private set; }
    /// <summary>Hồ sơ nhân viên đã tạo</summary>
    public EmployeeId? EmployeeId { get; private set; }
    public IReadOnlyList<OnboardingItem> Items => _items;

    public static Onboarding Create(CandidateId? candidateId, string fullName, string phone, string email, string position, Department department,
        EmployeeKind kind, BranchId branchId, DateOnly startDate, EmployeeId mentorId, IEnumerable<(string Key, OnboardingGroup Group, string Label)> checklist)
    {
        var o = new Onboarding
        {
            Id = OnboardingId.New(), CandidateId = candidateId, FullName = fullName, Phone = phone, Email = email, Position = position,
            Department = department, Kind = kind, BranchId = branchId, StartDate = startDate, MentorId = mentorId,
        };
        o._items.AddRange(checklist.Select(c => new OnboardingItem(c.Key, c.Group, c.Label)));
        return o;
    }

    public void Tick(string key, bool done, Guid by, DateTimeOffset at) => _items.FirstOrDefault(i => i.Key == key)?.Set(done, by, at);

    public Result LinkEmployee(EmployeeId employeeId)
    {
        if (EmployeeId is not null) return Error.Conflict("hr.onboarding.already_created", "Đã tạo hồ sơ nhân viên");
        EmployeeId = employeeId;
        return Result.Success();
    }
}
