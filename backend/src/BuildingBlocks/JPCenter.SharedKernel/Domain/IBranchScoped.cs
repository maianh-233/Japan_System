using JPCenter.SharedKernel.Primitives;

namespace JPCenter.SharedKernel.Domain;

/// <summary>Dữ liệu thuộc 1 chi nhánh — DbContext gốc tự lọc theo phạm vi quyền.</summary>
public interface IBranchScoped
{
    BranchId BranchId { get; }
}
