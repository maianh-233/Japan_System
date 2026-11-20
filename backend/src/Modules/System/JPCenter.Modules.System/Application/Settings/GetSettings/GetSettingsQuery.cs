using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Settings.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Settings.GetSettings;

/// <summary>CD-01 — xem Cài đặt chung (mọi nhân viên / GV đăng nhập đọc được: frontend cần lúc đăng nhập).</summary>
public sealed record GetSettingsQuery : IQuery<SettingsResponse>;

public sealed class GetSettingsHandler(SettingsReader reader) : IQueryHandler<GetSettingsQuery, SettingsResponse>
{
    public async Task<Result<SettingsResponse>> Handle(GetSettingsQuery query, CancellationToken ct) => await reader.ReadAsync(ct);
}
