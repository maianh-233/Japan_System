using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Common;

/// <summary>Hồ sơ nhân sự của người đang đăng nhập — trang cá nhân (chấm công của tôi, đơn của tôi) và người duyệt / người sửa công.</summary>
public sealed class CurrentEmployee(IHrDbContext db, ICurrentUser user)
{
    public Guid? Id => user.Access?.EmployeeId;

    public string Name => user.Access?.DisplayName ?? "Hệ thống";

    public async Task<Employee?> FindAsync(bool track, CancellationToken ct)
    {
        if (Id is not { } id) return null;
        var query = track ? db.Employees : db.Employees.AsNoTracking();
        return await query.FirstOrDefaultAsync(e => e.Id == new EmployeeId(id), ct);
    }

    /// <summary>
    /// Nhân viên văn phòng / làm theo ca (không phải GV / trợ giảng) — chấm công (CC-01.E1) và gửi đơn (DN-01.E7).
    /// Tài khoản không gắn hồ sơ → cùng lỗi <paramref name="notStaff"/>.
    /// </summary>
    public async Task<Result<Employee>> StaffAsync(Error notStaff, bool track, CancellationToken ct)
    {
        var me = await FindAsync(track, ct);
        if (me is null || me.IsTeaching) return notStaff;
        return me;
    }
}
