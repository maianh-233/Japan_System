namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Kết quả yêu cầu hoàn tiền: đã hoàn ngay (<see cref="Refund"/>) HOẶC đang chờ duyệt (<see cref="Request"/>)</summary>
public sealed record RefundOutcome(Refund? Refund, InvoiceRequest? Request);
