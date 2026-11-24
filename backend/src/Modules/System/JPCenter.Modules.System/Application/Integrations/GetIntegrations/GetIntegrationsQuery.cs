using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Integrations.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Integrations.GetIntegrations;

/// <summary>TH-01 — danh sách tích hợp theo thứ tự danh mục, khóa bí mật đã che, lượt dùng 30 ngày</summary>
public sealed record GetIntegrationsQuery : IQuery<IReadOnlyList<IntegrationDto>>;

public sealed class GetIntegrationsHandler(IntegrationReader reader) : IQueryHandler<GetIntegrationsQuery, IReadOnlyList<IntegrationDto>>
{
    public async Task<Result<IReadOnlyList<IntegrationDto>>> Handle(GetIntegrationsQuery query, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(ct));
}
