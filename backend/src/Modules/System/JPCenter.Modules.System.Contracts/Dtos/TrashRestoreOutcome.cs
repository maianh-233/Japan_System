namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>Kết quả khôi phục từ Thùng rác của module sở hữu</summary>
/// <param name="SourceFound">false = bản ghi gốc không còn (đã bị xóa hẳn)</param>
/// <param name="Note">Ghi chú cho người dùng, vd "Trùng tên — đổi thành Lễ tân (khôi phục)"</param>
public sealed record TrashRestoreOutcome(bool SourceFound, string? Note = null);
