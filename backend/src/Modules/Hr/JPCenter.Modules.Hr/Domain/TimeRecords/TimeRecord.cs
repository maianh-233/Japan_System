using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Domain.TimeRecords;

/// <summary>
/// Chấm công 1 ngày của nhân viên (check-in / check-out). Trạng thái ngày (muộn, sớm, vắng...) KHÔNG lưu — tính khi đọc bằng <see cref="AttendanceDay"/>.
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
    /// <summary>Người sửa tay (Hr.Employee) — quên chấm, lỗi máy...</summary>
    public Guid? EditedBy { get; private set; }
    /// <summary>Tên người sửa lúc sửa (tooltip "sửa bởi")</summary>
    public string? EditedByName { get; private set; }
    public string? EditReason { get; private set; }

    /// <summary>CC-01 — check-in (giờ server, giờ VN)</summary>
    public static TimeRecord CheckInAt(EmployeeId employeeId, BranchId branchId, DateOnly date, TimeOnly at) =>
        new() { Id = TimeRecordId.New(), EmployeeId = employeeId, BranchId = branchId, Date = date, CheckIn = at };

    /// <summary>CC-01 — check-out; bấm lại → cập nhật giờ ra</summary>
    public Result CheckOutAt(TimeOnly at)
    {
        if (CheckIn is not { } inAt) return TimeRecordErrors.NotCheckedIn;
        if (at < inAt) return TimeRecordErrors.InvalidCheckout;
        CheckOut = at;
        return Result.Success();
    }

    /// <summary>CC-04.E4 — sửa công ngày chưa có bản ghi → tạo bản ghi sửa tay</summary>
    public static Result<TimeRecord> Manual(EmployeeId employeeId, BranchId branchId, DateOnly date, TimeOnly? checkIn, TimeOnly? checkOut, Guid by,
        string byName, string reason, DateOnly today)
    {
        var r = new TimeRecord { Id = TimeRecordId.New(), EmployeeId = employeeId, BranchId = branchId, Date = date };
        var edit = r.Edit(checkIn, checkOut, by, byName, reason, today);
        return edit.IsFailure ? edit.Error : r;
    }

    /// <summary>CC-04 — quản lý sửa giờ vào / ra (bỏ trống được), bắt buộc lý do</summary>
    public Result Edit(TimeOnly? checkIn, TimeOnly? checkOut, Guid by, string byName, string reason, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(reason)) return TimeRecordErrors.ReasonRequired;
        if (Date > today) return TimeRecordErrors.FutureDate;
        if (checkIn is { } i && checkOut is { } o && o < i) return TimeRecordErrors.InvalidCheckout;
        (CheckIn, CheckOut, EditedBy, EditedByName, EditReason) = (checkIn, checkOut, by, byName, reason.Trim());
        return Result.Success();
    }
}
