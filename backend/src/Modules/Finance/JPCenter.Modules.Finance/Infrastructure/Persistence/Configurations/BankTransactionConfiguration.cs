using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Finance.Infrastructure.Persistence.Configurations;

/// <summary>finance.bank_transactions — database.md bảng 74. UQ <c>ref</c> (webhook gửi lại không tạo trùng — TC-10.E1); hóa đơn đã khớp lưu uuid[].</summary>
internal sealed class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> b)
    {
        b.ToTable("bank_transactions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Ref).IsUnique();
        b.HasIndex(x => new { x.BranchId, x.Status, x.At });
        b.Property(x => x.Ref).HasMaxLength(100);
        b.Property(x => x.Content).HasMaxLength(500);
        b.Property(x => x.Payer).HasMaxLength(200);
        b.Property(x => x.Account).HasMaxLength(100);
        b.Property(x => x.Note).HasMaxLength(500);
    }
}

/// <summary>finance.payment_reminders — database.md bảng 75. Chỉ thêm; <c>invoice_id</c> → invoices.</summary>
internal sealed class PaymentReminderConfiguration : IEntityTypeConfiguration<PaymentReminder>
{
    public void Configure(EntityTypeBuilder<PaymentReminder> b)
    {
        b.ToTable("payment_reminders");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.InvoiceId, x.At });
        b.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Note).HasMaxLength(500);
    }
}
