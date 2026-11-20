namespace JPCenter.Modules.System.Application.Trash.Common;

/// <summary>1 mục Thùng rác (TR-01) — khớp <c>TrashItem</c> của <c>frontend/src/features/system/trash.ts</c> (không có bản sao dữ liệu).</summary>
/// <param name="Kind">"module.entity", vd system.role</param>
/// <param name="DeletedBy">Tên người xóa ("Hệ thống" khi job / không rõ)</param>
/// <param name="DaysLeft">Còn bao nhiêu ngày trước khi xóa hẳn (0 = đã hết hạn, chờ job dọn)</param>
/// <param name="Restorable">Có module nhận khôi phục loại này</param>
public sealed record TrashItemDto(Guid Id, string Kind, string Label, string? Detail, Guid? BranchId, DateTimeOffset DeletedAt,
    string DeletedBy, DateTimeOffset PurgeAfter, int DaysLeft, bool Restorable);
