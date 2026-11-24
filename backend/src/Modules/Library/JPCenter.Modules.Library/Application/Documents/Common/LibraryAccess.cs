using JPCenter.Application.Common.Security;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Application.Documents.Common;

/// <summary>
/// Phạm vi quyền thư viện (user story 16): <c>library.documents</c>. Tài liệu có nhiều chi nhánh (rỗng = mọi chi nhánh) nên không dùng
/// global filter theo 1 <c>branch_id</c> — phạm vi <c>branch</c> = tài liệu dùng chung + tài liệu có 1 chi nhánh mình được gán (TV-01.E1).
/// </summary>
public sealed class LibraryAccess(ICurrentUser user)
{
    public const string Page = "library.documents";

    public Guid ActorId => user.Access?.EmployeeId ?? user.AccountId ?? Guid.Empty;

    /// <summary>Chi nhánh được thao tác; null = mọi chi nhánh; rỗng = không có quyền</summary>
    public HashSet<Guid>? Branches(PermissionAction action) => user.AllowedBranches(Page, action);

    /// <summary>Tài liệu nằm trong phạm vi của hành động</summary>
    public bool Covers(LibraryDocument doc, PermissionAction action) =>
        Branches(action) is not { } allowed || doc.BranchIds.Count == 0 || doc.BranchIds.Any(b => allowed.Contains(b.Value));

    /// <summary>Chi nhánh gán cho tài liệu phải nằm trong phạm vi; phạm vi <c>branch</c> không tạo được tài liệu dùng chung mọi chi nhánh</summary>
    public bool CanAssign(IReadOnlyCollection<Guid> branchIds, PermissionAction action) =>
        Branches(action) is not { } allowed || (branchIds.Count > 0 && branchIds.All(allowed.Contains));

    public static readonly Error BranchNotAllowed = Error.Forbidden("library.branch_not_allowed",
        "Bạn chỉ gán được tài liệu cho chi nhánh mình được phân công");
}
