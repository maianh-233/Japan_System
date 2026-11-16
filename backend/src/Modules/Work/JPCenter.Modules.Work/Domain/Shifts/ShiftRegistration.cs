using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>
/// Nhân viên tự đăng ký: ca mong muốn tuần tới / báo rảnh / báo bận (chưa khai không có nghĩa là bận).
/// Bảng: work.shift_registrations, UQ (employee_id, date, shift, kind)
/// </summary>
public sealed class ShiftRegistration : AggregateRoot<ShiftRegistrationId>
{
    private ShiftRegistration() { }

    public Guid EmployeeId { get; private set; }
    public DateOnly Date { get; private set; }
    public ShiftKey Shift { get; private set; }
    public ShiftRegistrationKind Kind { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ShiftRegistration Create(Guid employeeId, DateOnly date, ShiftKey shift, ShiftRegistrationKind kind, DateTimeOffset at) =>
        new() { Id = ShiftRegistrationId.New(), EmployeeId = employeeId, Date = date, Shift = shift, Kind = kind, UpdatedAt = at };

    public void Change(ShiftRegistrationKind kind, DateTimeOffset at) => (Kind, UpdatedAt) = (kind, at);
}
