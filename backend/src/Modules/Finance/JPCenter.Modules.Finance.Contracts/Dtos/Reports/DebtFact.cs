namespace JPCenter.Modules.Finance.Contracts.Dtos.Reports;

/// <summary>Hóa đơn đang mở còn nợ — công nợ, tuổi nợ (BC-04), hóa đơn đến hạn (BC-02 Kế toán).</summary>
/// <param name="StudentId">Students.Student</param>
/// <param name="ClassId">Academic.Class</param>
/// <param name="DueDate">Hạn đang áp dụng (đợt đang tới hạn khi đóng nhiều đợt)</param>
/// <param name="Aging">not_due · days1_to30 · days31_to60 · over60</param>
public sealed record DebtFact(Guid InvoiceId, string Code, Guid BranchId, Guid StudentId, Guid ClassId, DateOnly DueDate, decimal Remaining,
    string Aging, bool Overdue);
