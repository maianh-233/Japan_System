using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Approvals.DecideApproval;
using JPCenter.Modules.System.Application.Approvals.GetApprovals;
using JPCenter.Modules.System.Application.Approvals.WithdrawApproval;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>
/// PQ-09 — duyệt 2 người. Endpoint chỉ đòi quyền trang Duyệt thao tác; quyền theo loại yêu cầu (vd cấp quyền tài chính cần
/// thêm Nhật ký hoặc Phân quyền người dùng) kiểm trong handler vì phụ thuộc dữ liệu.
/// </summary>
public static class ApprovalEndpoints
{
    private const string Page = "system.approvals";

    public sealed record NoteBody(string? Note);

    public static void Map(IEndpointRouteBuilder system)
    {
        var group = system.MapGroup("/approvals");

        group.MapGet("/", async (string? status, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetApprovalsQuery(status), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        group.MapPost("/{id:guid}/approve", async (Guid id, NoteBody? body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DecideApprovalCommand(id, true, body?.Note), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        group.MapPost("/{id:guid}/reject", async (Guid id, NoteBody body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DecideApprovalCommand(id, false, body.Note), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        // Người tạo rút lại — không cần quyền trang Duyệt (ai gửi được thì rút được)
        group.MapPost("/{id:guid}/withdraw", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new WithdrawApprovalCommand(id), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff, Portal.Teacher);
    }
}
