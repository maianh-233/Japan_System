namespace JPCenter.SharedKernel.Events;

/// <summary>
/// Sự kiện module này báo cho module khác (vd <c>EmployeeQuit</c> của Hr → System khóa tài khoản).
/// Khai báo trong <c>&lt;Module&gt;.Contracts/IntegrationEvents</c> dạng <c>sealed record</c>, chỉ chứa kiểu nguyên thủy / Guid
/// (sẽ được lưu JSON vào outbox). Đổi tên / bỏ trường = phá vỡ sự kiện đang chờ gửi → chỉ THÊM trường có giá trị mặc định.
/// </summary>
public interface IIntegrationEvent;
