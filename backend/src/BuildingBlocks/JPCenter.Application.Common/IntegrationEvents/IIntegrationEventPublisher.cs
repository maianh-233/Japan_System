using JPCenter.SharedKernel.Events;

namespace JPCenter.Application.Common.IntegrationEvents;

/// <summary>
/// Phát sự kiện cho module khác qua Outbox. Gọi TRƯỚC <c>SaveChangesAsync</c> của module mình:
/// sự kiện được ghi vào bảng <c>outbox_messages</c> trong CÙNG giao dịch với dữ liệu — lưu lỗi thì không phát,
/// lưu xong thì chắc chắn được gửi (kể cả app tắt giữa chừng, gửi khi chạy lại).
/// </summary>
public interface IIntegrationEventPublisher
{
    void Publish<TEvent>(TEvent @event) where TEvent : IIntegrationEvent;
}
