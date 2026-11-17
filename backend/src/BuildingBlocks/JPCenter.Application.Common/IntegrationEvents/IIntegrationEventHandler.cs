using JPCenter.SharedKernel.Events;

namespace JPCenter.Application.Common.IntegrationEvents;

/// <summary>
/// Xử lý sự kiện của module khác — đặt trong <c>Application/&lt;Feature&gt;/EventHandlers/</c> của module nhận;
/// tự được đăng ký bởi <c>AddHandlersFromAssembly</c>. Chạy NỀN (không có người dùng đăng nhập), mỗi handler 1 scope riêng.
/// <para>Phải <b>chịu được chạy lại</b>: lỗi → thử lại sau; kiểm tra trạng thái trước khi làm (vd tài khoản đã khóa thì bỏ qua).
/// Ném exception = thất bại, sẽ thử lại. Handler khác của cùng sự kiện đã xong thì không chạy lại.</para>
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task Handle(TEvent @event, CancellationToken ct);
}
