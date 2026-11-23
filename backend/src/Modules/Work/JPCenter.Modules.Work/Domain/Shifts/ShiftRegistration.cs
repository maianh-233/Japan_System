using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>
/// XC-04 — nhân viên tự báo rảnh / bận từng ca (nguyện vọng, không thay lịch ca). Bỏ chọn = xóa bản ghi. Ngày đã qua không sửa.
/// Bảng: work.shift_registrations, UQ (employee_id, date, shift)
/// </summary>
public sealed class ShiftRegistration : AggregateRoot<ShiftRegistrationId>
{
    private ShiftRegistration() { }

    /// <summary>Hr.Employee</summary>
    public Guid EmployeeId { get; private set; }
    public DateOnly Date { get; private set; }
    public ShiftKey Shift { get; private set; }
    public ShiftRegistrationKind Kind { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<ShiftRegistration> Create(Guid employeeId, DateOnly date, ShiftKey shift, ShiftRegistrationKind kind, DateTimeOffset at, DateOnly today)
    {
        if (date < today) return ShiftErrors.PastDate;
        return new ShiftRegistration { Id = ShiftRegistrationId.New(), EmployeeId = employeeId, Date = date, Shift = shift, Kind = kind, UpdatedAt = at };
    }

    public Result Change(ShiftRegistrationKind kind, DateTimeOffset at, DateOnly today)
    {
        if (Date < today) return ShiftErrors.PastDate;
        (Kind, UpdatedAt) = (kind, at);
        return Result.Success();
    }

    public Result CanRemove(DateOnly today) => Date < today ? ShiftErrors.PastDate : Result.Success();
}
