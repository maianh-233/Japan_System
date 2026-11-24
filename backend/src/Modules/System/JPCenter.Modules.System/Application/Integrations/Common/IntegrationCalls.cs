using System.Security.Cryptography;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Integrations.Common;

/// <summary>
/// Dùng chung cho kiểm tra kết nối (TH-04), gửi tin (TH-06), tạo phòng (TH-08), webhook (TH-07): tải tích hợp (có theo dõi), giải mã cấu hình,
/// ghi kết quả gọi (<see cref="Integration.RecordCall"/> + 1 dòng nhật ký). Người gọi tự <c>SaveChangesAsync</c>.
/// </summary>
public sealed class IntegrationCalls(ISystemDbContext db, IIntegrationSecrets secrets, IIntegrationGateway gateway, IClock clock)
{
    public IIntegrationGateway Gateway => gateway;

    public Task<Integration?> FindAsync(string key, CancellationToken ct) => db.Integrations.FirstOrDefaultAsync(i => i.Key == key, ct);

    public async Task<IReadOnlyList<Integration>> AllAsync(CancellationToken ct) => await db.Integrations.ToListAsync(ct);

    /// <summary>Cấu hình đã giải mã. Khóa giải mã bị mất (đổi máy, mất thư mục khóa) → trường bí mật rỗng, phải kết nối lại.</summary>
    public IReadOnlyDictionary<string, string> ConfigOf(Integration integration) =>
        integration.Config.ToDictionary(kv => kv.Key, kv => integration.Spec.Field(kv.Key)?.Secret == true ? Reveal(kv.Value) : kv.Value);

    public string Reveal(string value)
    {
        try
        {
            return secrets.Reveal(value);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }

    /// <summary>Ghi kết quả gọi: cập nhật trạng thái tích hợp (lỗi xác thực → <c>error</c>) + thêm nhật ký. Chế độ thử có hậu tố "(thử)".</summary>
    public void Record(Integration integration, GatewayResult result) =>
        db.IntegrationLogs.Add(integration.RecordCall(result.Ok, LogText(result), clock.UtcNow, result.AuthFailure));

    /// <summary>Nội dung ghi nhật ký: chế độ thử thêm hậu tố "(thử)"</summary>
    public static string LogText(GatewayResult result) =>
        result.Sandbox && !result.Message.Contains("(thử)", StringComparison.Ordinal) ? result.Message + " (thử)" : result.Message;

    /// <summary>Ghi nhật ký không gọi dịch vụ (vd "Không có kênh gửi", webhook nhận giao dịch)</summary>
    public void Note(Integration integration, bool ok, string message) =>
        db.IntegrationLogs.Add(IntegrationLog.Create(integration.Id, ok, message, clock.UtcNow));

    public Task<int> SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
