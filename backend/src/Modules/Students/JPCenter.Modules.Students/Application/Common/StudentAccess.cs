using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Application.Students.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Students.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Students.Application.Common;

/// <summary>
/// Phạm vi quyền Học viên (PQ-10): học viên theo chi nhánh CỦA HỌC VIÊN (HV-02.E2), ghi danh còn xem / thao tác được từ trang Lớp học
/// theo chi nhánh CỦA LỚP. Thao tác ghi danh đứng ở 2 trang: <c>academic.classes.update</c> (lớp) hoặc <c>students.profiles.update</c> (hồ sơ HV).
/// Phạm vi <c>own</c> = HV có lead liên kết do mình phụ trách (Crm.Contracts <c>ILeadOwnership</c>) trong chi nhánh được gán.
/// </summary>
public sealed class StudentAccess(ICurrentUser user, JPCenter.Modules.Crm.Contracts.ILeadOwnership leads)
{
    public const string Profiles = "students.profiles";
    public const string Classes = "academic.classes";
    /// <summary>Cảnh báo bỏ học & chăm sóc (HV-14)</summary>
    public const string AtRisk = "students.at-risk";

    public Guid ActorId => user.Access?.EmployeeId ?? user.AccountId ?? Guid.Empty;

    public bool Can(string page, PermissionAction action) => user.Can(page, action);

    /// <summary>Chi nhánh được thao tác; null = mọi chi nhánh; rỗng = không có quyền</summary>
    public HashSet<Guid>? Branches(string page, PermissionAction action)
    {
        var perm = user.Permission(page, action);
        if (perm is null) return [];
        if (perm.Scope == PermissionScope.All) return null;
        if (perm.BranchIds.Count > 0) return perm.BranchIds.ToHashSet();
        var me = user.Access;
        return me is null || me.AllBranches ? null : me.BranchIds.ToHashSet();
    }

    /// <summary>Phạm vi <c>own</c>: Id các HV có lead do mình phụ trách; null = không giới hạn theo người</summary>
    public async Task<HashSet<Guid>?> OwnStudentsAsync(string page, PermissionAction action, CancellationToken ct) =>
        user.Permission(page, action) is { Scope: PermissionScope.Own }
            ? (await leads.StudentIdsOfSaleAsync(user.Access?.EmployeeId ?? Guid.Empty, ct)).ToHashSet()
            : null;

    /// <summary>HV trong phạm vi của hành động: đúng chi nhánh và (phạm vi own) có lead do mình phụ trách</summary>
    public async Task<bool> CoversAsync(string page, PermissionAction action, Guid studentId, Guid branchId, CancellationToken ct) =>
        InBranch(page, action, branchId) && (await OwnStudentsAsync(page, action, ct) is not { } own || own.Contains(studentId));

    public bool InBranch(string page, PermissionAction action, Guid branchId) =>
        Branches(page, action) is not { } allowed || allowed.Contains(branchId);

    /// <summary>Xem ghi danh: xem được hồ sơ HV hoặc xem được lớp</summary>
    public bool CanSeeEnrollment(Guid classBranch, Guid studentBranch) =>
        InBranch(Classes, PermissionAction.View, classBranch) || InBranch(Profiles, PermissionAction.View, studentBranch);

    /// <summary>Ghi danh / bảo lưu / chuyển / cho nghỉ: sửa được lớp hoặc sửa được hồ sơ HV</summary>
    public bool CanManageEnrollment(Guid classBranch, Guid studentBranch) =>
        InBranch(Classes, PermissionAction.Update, classBranch) || InBranch(Profiles, PermissionAction.Update, studentBranch);

    public static readonly Error BranchNotAllowed = Error.Forbidden("students.branch_not_allowed",
        "Bạn chỉ thao tác được học viên / lớp của chi nhánh mình được phân công");
}
