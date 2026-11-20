using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Contracts;

/// <summary>
/// Module sở hữu 1 loại dữ liệu xóa mềm hiện thực interface này (đăng ký DI) để Thùng rác khôi phục / xóa hẳn được (TR-02 → TR-04).
/// Mục được thêm vào Thùng rác bằng integration event <c>ItemTrashed</c> (cùng giao dịch với lệnh xóa).
/// </summary>
public interface ITrashHandler
{
    /// <summary>Loại dữ liệu "module.entity" — trùng <c>trash_items.kind</c>, vd <c>system.role</c>, <c>crm.message_template</c></summary>
    string Kind { get; }

    /// <summary>Bỏ cờ xóa mềm. Lỗi nghiệp vụ → <c>Result</c> thất bại (TR-02.E3); bản ghi không còn → <c>SourceFound = false</c> (TR-02.E4).</summary>
    Task<Result<TrashRestoreOutcome>> RestoreAsync(Guid entityId, CancellationToken ct);

    /// <summary>Xóa hẳn bản ghi (TR-03, TR-04). Không còn bản ghi thì coi như xong.</summary>
    Task PurgeAsync(Guid entityId, CancellationToken ct);
}
