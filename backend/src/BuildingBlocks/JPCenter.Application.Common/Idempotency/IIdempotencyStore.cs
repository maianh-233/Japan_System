namespace JPCenter.Application.Common.Idempotency;

/// <summary>
/// Lưu kết quả request theo <c>Idempotency-Key</c> để request gửi lại không chạy lần 2 (request-pipeline.md mục 5).
/// <paramref name="scope"/> = người gửi (Id tài khoản hoặc IP) — key chỉ có nghĩa trong phạm vi 1 người.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyBegin> TryBeginAsync(string scope, string key, string requestHash, CancellationToken ct);

    Task CompleteAsync(string scope, string key, StoredResponse response, CancellationToken ct);

    /// <summary>Lỗi hệ thống → bỏ đánh dấu để client thử lại cùng key.</summary>
    Task ReleaseAsync(string scope, string key, CancellationToken ct);
}

public enum IdempotencyState
{
    /// <summary>Lần đầu — được chạy</summary>
    Started,
    /// <summary>Lần trước còn đang chạy</summary>
    InProgress,
    /// <summary>Đã xong — trả lại response cũ</summary>
    Completed,
    /// <summary>Cùng key nhưng nội dung khác</summary>
    Mismatch,
}

public sealed record IdempotencyBegin(IdempotencyState State, StoredResponse? Response = null);

public sealed record StoredResponse(int StatusCode, string? Body, string? ContentType);
