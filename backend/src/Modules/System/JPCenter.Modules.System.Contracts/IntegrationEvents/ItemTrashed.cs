using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.System.Contracts.IntegrationEvents;

/// <summary>
/// Module khác vừa xóa mềm 1 bản ghi → System thêm mục Thùng rác (hạn giữ theo Cài đặt chung lúc nhận). Phát cùng giao dịch với lệnh xóa.
/// Module phát phải có <see cref="ITrashHandler"/> cùng <paramref name="Kind"/> để khôi phục được.
/// </summary>
/// <param name="Kind">"module.entity", vd crm.message_template</param>
/// <param name="EntityId">Id bản ghi gốc</param>
/// <param name="BranchId">System.Branch — null = dữ liệu dùng chung</param>
/// <param name="DeletedBy">System.UserAccount — Guid.Empty = hệ thống</param>
public sealed record ItemTrashed(string Kind, Guid EntityId, string Label, string? Detail, Guid? BranchId, Guid DeletedBy, DateTimeOffset DeletedAt)
    : IIntegrationEvent;
