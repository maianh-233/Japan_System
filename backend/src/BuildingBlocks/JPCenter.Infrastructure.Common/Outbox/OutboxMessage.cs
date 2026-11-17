namespace JPCenter.Infrastructure.Common.Outbox;

/// <summary>
/// 1 sự kiện chờ gửi cho module khác. Bảng: <c>&lt;schema module&gt;.outbox_messages</c> — mọi module đều có (ModuleDbContextBase tự thêm).
/// Ghi trong cùng giao dịch với dữ liệu nghiệp vụ; <see cref="OutboxProcessor{TContext}"/> đọc và gửi.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    /// <summary>Tên đầy đủ kiểu sự kiện, vd JPCenter.Modules.Hr.Contracts.IntegrationEvents.EmployeeQuit</summary>
    public string Type { get; set; } = null!;
    /// <summary>Nội dung sự kiện (JSON)</summary>
    public string Payload { get; set; } = null!;
    public DateTimeOffset OccurredAt { get; set; }
    /// <summary>Gửi xong cho mọi handler</summary>
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    /// <summary>Lần thử kế tiếp (lùi dần khi lỗi)</summary>
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    /// <summary>Handler đã xử lý xong — thử lại chỉ chạy handler còn lỗi</summary>
    public List<string> HandledBy { get; set; } = [];
    /// <summary>Hết số lần thử — cần người xem (log lỗi); không tự gửi nữa</summary>
    public DateTimeOffset? FailedAt { get; set; }
}
