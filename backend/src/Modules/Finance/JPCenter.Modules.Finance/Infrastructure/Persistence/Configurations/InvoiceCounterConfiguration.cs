using JPCenter.Modules.Finance.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Finance.Infrastructure.Persistence.Configurations;

/// <summary>finance.invoice_counters — database.md bảng 68a. UQ (branch_id, period) để cập nhật nguyên tử "lấy số kế tiếp".</summary>
internal sealed class InvoiceCounterConfiguration : IEntityTypeConfiguration<InvoiceCounter>
{
    public void Configure(EntityTypeBuilder<InvoiceCounter> b)
    {
        b.ToTable("invoice_counters");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.BranchId, x.Period }).IsUnique();
        b.Property(x => x.Period).HasMaxLength(10);
    }
}
