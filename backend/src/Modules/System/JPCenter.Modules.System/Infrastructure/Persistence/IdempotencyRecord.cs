namespace JPCenter.Modules.System.Infrastructure.Persistence;

/// <summary>
/// Bảng: system.idempotency_keys — kết quả request theo Idempotency-Key (request-pipeline.md mục 5).
/// Dữ liệu kỹ thuật, không phải nghiệp vụ → nằm ở Infrastructure, không có trong Domain.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>Người gửi: "user:{id}" hoặc "ip:{ip}"</summary>
    public string Scope { get; set; } = null!;
    public string Key { get; set; } = null!;
    /// <summary>SHA-256 nội dung request — cùng key khác nội dung → 422</summary>
    public string RequestHash { get; set; } = null!;
    /// <summary>null = đang xử lý</summary>
    public int? StatusCode { get; set; }
    public string? Body { get; set; }
    public string? ContentType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
