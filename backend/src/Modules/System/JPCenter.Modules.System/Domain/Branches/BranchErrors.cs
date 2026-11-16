using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Branches;

public static class BranchErrors
{
    public static readonly Error CodeRequired = Error.Validation("system.branch.code_required", "Chưa nhập mã chi nhánh");
    public static readonly Error NameRequired = Error.Validation("system.branch.name_required", "Chưa nhập tên chi nhánh");
    public static readonly Error InvalidStatus = Error.Conflict("system.branch.invalid_status", "Không chuyển được trạng thái chi nhánh");
}
