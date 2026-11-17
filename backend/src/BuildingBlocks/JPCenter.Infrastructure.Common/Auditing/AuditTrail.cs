using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Infrastructure.Common.Auditing;

/// <summary>Dựng <see cref="AuditEntry"/> từ ngữ cảnh request rồi đẩy vào hàng đợi.</summary>
public sealed class AuditTrail(IAuditWriter writer, IClock clock, ICurrentUser user, IRequestInfo request, IAuditChangeCollector changes)
    : IAuditTrail
{
    public void Record(AuditCategory category, AuditSeverity severity, string action, string target,
        string? detail = null, AuditActor? actor = null, Guid? branchId = null)
    {
        var who = actor ?? (user.Access is { } a
            ? new AuditActor(a.AccountId, a.DisplayName, a.RoleName)
            : new AuditActor(null, "Hệ thống", "Hệ thống"));

        writer.Enqueue(new AuditEntry(clock.UtcNow, who.Id, who.Name, who.Role, branchId, category, severity, action, target, detail,
            changes.Drain(), request.Ip, request.Device, request.TraceId));
    }
}
