using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Guardians.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Guardians.GetGuardians;

/// <summary>HV-12 bước 1 — danh sách phụ huynh / người giám hộ trong phạm vi (cả lead đã ẩn — trang tự lọc)</summary>
public sealed record GetGuardiansQuery : IQuery<IReadOnlyList<GuardianDto>>;

public sealed class GetGuardiansHandler(GuardianAccess access, LeadReader reader) : IQueryHandler<GetGuardiansQuery, IReadOnlyList<GuardianDto>>
{
    public async Task<Result<IReadOnlyList<GuardianDto>>> Handle(GetGuardiansQuery query, CancellationToken ct)
    {
        var leads = await access.ListAsync(PermissionAction.View, ct);
        var meta = await reader.MetaAsync(leads.Select(l => l.Id).ToList(), ct);
        return leads.Select(l => GuardianMapping.ToDto(l, meta.GetValueOrDefault(l.Id).Created, meta.GetValueOrDefault(l.Id).Version)).ToList();
    }
}
