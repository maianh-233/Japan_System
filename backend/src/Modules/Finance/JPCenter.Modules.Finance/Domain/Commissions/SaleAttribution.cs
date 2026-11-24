namespace JPCenter.Modules.Finance.Domain.Commissions;

/// <summary>
/// TC-04.E5 — chọn Sale được ghi nhận doanh số khi lập hóa đơn: Sale lead đã chốt › Sale lead mới nhất › chia đều Sale đang làm ở chi nhánh
/// (cố định theo HV — cùng HV luôn ra cùng Sale) › null. Gán tay của kế toán không đi qua đây (ghi đè ở <c>Invoice.ReassignSale</c>).
/// </summary>
public static class SaleAttribution
{
    /// <param name="wonLeadSaleId">Sale phụ trách lead đã chốt liên kết với HV (Crm)</param>
    /// <param name="latestLeadSaleId">Sale phụ trách lead liên kết mới nhất (Crm)</param>
    /// <param name="branchSaleIds">Sale đang làm ở chi nhánh của hóa đơn (Hr)</param>
    public static Guid? Pick(Guid studentId, Guid? wonLeadSaleId, Guid? latestLeadSaleId, IEnumerable<Guid> branchSaleIds)
    {
        if (wonLeadSaleId is { } won) return won;
        if (latestLeadSaleId is { } latest) return latest;
        var sales = branchSaleIds.Distinct().Order().ToList();
        if (sales.Count == 0) return null;
        // Chia đều cố định theo HV: Guid v7 đầu là thời gian, cuối là ngẫu nhiên → lấy 4 byte cuối làm "băm"
        var bytes = studentId.ToByteArray();
        var bucket = (int)(BitConverter.ToUInt32(bytes, bytes.Length - 4) % (uint)sales.Count);
        return sales[bucket];
    }
}
