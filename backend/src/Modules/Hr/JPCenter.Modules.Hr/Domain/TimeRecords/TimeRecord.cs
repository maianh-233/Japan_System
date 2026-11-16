using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Domain.TimeRecords;

/// <summary>
/// Chấm công 1 ngày của nhân viên (check-in / check-out). So với giờ hành chính / ca được xếp → muộn, về sớm, vắng (tính ở Application).
/// Bảng: hr.time_records, UQ (employee_id, date)
/// </summary>
public sealed class TimeRecord : AggregateRoot<TimeRecordId>, IBranchScoped, IAuditable
{
    private TimeRecord() { }

    public EmployeeId EmployeeId { get; private set; }
    public BranchId BranchId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly? CheckIn { get; private set; }
    public TimeOnly? CheckOut { get; private set; }
    /// <summary>Sửa tay bởi quản lý (quên chấm, lỗi máy...)</summary>
    public Guid? EditedBy { get; private set; }
    public string? EditReason { get; private set; }

    public static TimeRecord CheckInAt(EmployeeId employeeId, BranchId branchId, DateOnly date, TimeOnly at) =>
        new() { Id = TimeRecordId.New(), EmployeeId = employeeId, BranchId = branchId, Date = date, CheckIn = at };

    public Result CheckOutAt(TimeOnly at)
    {
        if (CheckIn is { } inAt && at < inAt) return Error.Validation("hr.time_record.invalid_checkout", "Giờ ra phải sau giờ vào");
        CheckOut = at;
        return Result.Success();
    }

    public Result Edit(TimeOnly? checkIn, TimeOnly? checkOut, Guid by, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return Error.Validation("hr.time_record.reason_required", "Cần ghi lý do sửa chấm công");
        (CheckIn, CheckOut, EditedBy, EditReason) = (checkIn, checkOut, by, reason);
        return Result.Success();
    }
}
