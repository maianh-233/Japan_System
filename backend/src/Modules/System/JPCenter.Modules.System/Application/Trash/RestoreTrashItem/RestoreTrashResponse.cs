namespace JPCenter.Modules.System.Application.Trash.RestoreTrashItem;

/// <summary>Kết quả khôi phục (TR-02) — frontend tải lại dữ liệu loại <paramref name="Kind"/></summary>
/// <param name="Note">Ghi chú của module sở hữu, vd đổi tên vì trùng (PQ-04.E3)</param>
public sealed record RestoreTrashResponse(string Kind, Guid EntityId, string? Note);
