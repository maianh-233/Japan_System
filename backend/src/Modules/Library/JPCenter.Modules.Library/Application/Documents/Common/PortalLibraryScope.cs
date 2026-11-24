using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Application.Documents.Common;

/// <summary>Người dùng cổng thấy gì: đối tượng (HV / PH → student, GV → teacher), chi nhánh + cấp độ các lớp đang học / đang dạy</summary>
public sealed record PortalLibraryScope(DocAudience Audience, IReadOnlyList<BranchId> Branches, IReadOnlySet<JlptLevel> Levels)
{
    public bool Sees(LibraryDocument d) => d.IsVisibleOnPortal(Audience, Branches);
}
