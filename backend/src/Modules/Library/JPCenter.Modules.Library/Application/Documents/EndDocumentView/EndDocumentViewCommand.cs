using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Application.Documents.EndDocumentView;

/// <summary>TV-05 bước 2 — đóng trình xem (nhân viên / GV / HV / PH): chỉ kết thúc được lượt của chính mình (E3); kết thúc lần 2 bỏ qua</summary>
public sealed record EndDocumentViewCommand(Guid ViewId) : ICommand;

public sealed class EndDocumentViewHandler(LibraryViewer viewer, DocumentViews views) : ICommandHandler<EndDocumentViewCommand>
{
    public async Task<Result> Handle(EndDocumentViewCommand cmd, CancellationToken ct) =>
        await viewer.CurrentAsync(ct) is { } me ? await views.EndAsync(cmd.ViewId, me, ct) : LibraryDocumentErrors.ViewNotFound;
}
