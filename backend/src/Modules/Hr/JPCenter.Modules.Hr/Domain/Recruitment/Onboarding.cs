using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>
/// Nhận việc (OB-01, OB-02): checklist giấy tờ / tài khoản / thiết bị / đào tạo; "Tạo hồ sơ" → gắn hồ sơ nhân viên.
/// Xong = đã gắn hồ sơ + tick đủ. Bảng: hr.onboardings (+ onboarding_items)
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

    /// <summary>% mục đã tick</summary>
    public int Progress => _items.Count == 0 ? 100 : _items.Count(i => i.Done) * 100 / _items.Count;
    public bool IsComplete => EmployeeId is not null && _items.All(i => i.Done);

    public static Result<Onboarding> Create(CandidateId? candidateId, string fullName, string phone, string email, string position, Department department,
        EmployeeKind kind, BranchId branchId, DateOnly startDate, EmployeeId mentorId)
    {
        if (new[] { fullName, phone, email, position }.Any(string.IsNullOrWhiteSpace)) return RecruitmentErrors.OnboardingInfo;
        var o = new Onboarding
        {
            Id = OnboardingId.New(), CandidateId = candidateId, FullName = fullName.Trim(), Phone = phone.Trim(), Email = email.Trim().ToLowerInvariant(),
            Position = position.Trim(), Department = kind == EmployeeKind.Staff ? department : Department.Teaching, Kind = kind, BranchId = branchId,
            StartDate = startDate, MentorId = mentorId,
        };
        o._items.AddRange(OnboardingTemplate.For(kind).Select(c => new OnboardingItem(c.Key, c.Group, c.Label)));
        return o;
    }

    /// <summary>Tick / bỏ tick 1 mục (ghi người + giờ)</summary>
    public Result Tick(string key, bool done, Guid by, string byName, DateTimeOffset at)
    {
        if (_items.FirstOrDefault(i => i.Key == key) is not { } item) return RecruitmentErrors.UnknownItem;
        item.Set(done, by, byName, at);
        return Result.Success();
    }

    /// <summary>OB-01 — System đã cấp tài khoản cho hồ sơ đã gắn → tự tick "Tạo tài khoản đăng nhập". Trả true nếu vừa tick.</summary>
    public bool MarkAccountCreated(DateTimeOffset at)
    {
        if (_items.FirstOrDefault(i => i.Key == OnboardingTemplate.AccountKey) is not { Done: false } item) return false;
        item.SetBySystem("Hệ thống", at);
        return true;
    }

    /// <summary>OB-02 — gắn hồ sơ vừa tạo</summary>
    public Result LinkEmployee(EmployeeId employeeId)
    {
        if (EmployeeId is not null) return RecruitmentErrors.AlreadyCreated;
        EmployeeId = employeeId;
        return Result.Success();
    }
}
