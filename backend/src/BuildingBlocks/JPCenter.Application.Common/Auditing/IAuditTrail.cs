using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Auditing;

/// <summary>
/// Ghi 1 dòng nhật ký: tự điền thời điểm, người thao tác, IP, thiết bị, traceId và các thay đổi dữ liệu EF thu được trong request.
/// Dùng trong handler (vd đăng nhập thất bại) hoặc qua filter <c>.Audited(...)</c> của endpoint.
/// </summary>
public interface IAuditTrail
{
    /// <param name="actor">Ghi đè người thao tác khi chưa đăng nhập (vd đăng nhập sai: id + tên tài khoản bị thử).</param>
    void Record(AuditCategory category, AuditSeverity severity, string action, string target,
        string? detail = null, AuditActor? actor = null, Guid? branchId = null);
}

/// <summary>Người thao tác ghi vào nhật ký (tên / vai trò lưu cứng tại thời điểm thao tác).</summary>
public sealed record AuditActor(Guid? Id, string Name, string Role);
