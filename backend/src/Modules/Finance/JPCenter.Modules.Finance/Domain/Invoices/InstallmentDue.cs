using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>1 đợt phải đóng của hóa đơn trả nhiều đợt (TC-02): số tiền + hạn riêng. Lưu jsonb <c>finance.invoices.schedule</c>.</summary>
public sealed record InstallmentDue(Money Amount, DateOnly DueDate);
