using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Finance.Infrastructure.Persistence.Configurations;

/// <summary>
/// finance.invoices + payments + refunds + invoice_requests — database.md bảng 68, 69, 70, 70a.
/// <c>enrollment_id</c> UQ (mỗi ghi danh 1 hóa đơn — TC-04.E1); học viên / lớp / người thu không FK (schema khác);
/// <c>discount_id</c> → discounts, <c>installment_plan_id</c> → installment_plans. Số liệu tiền (đã thu, còn nợ…) không lưu.
/// </summary>
internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ToTable("invoices");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.EnrollmentId).IsUnique();
        b.HasIndex(x => x.TransferContent).IsUnique();
        b.HasIndex(x => x.StudentId);
        b.HasIndex(x => x.ClassId);
        b.HasIndex(x => new { x.BranchId, x.Status, x.DueDate });
        b.Property(x => x.Code).HasMaxLength(50);
        b.Property(x => x.TransferContent).HasMaxLength(InvoiceCode.MaxTransferContent);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.Property(x => x.VoidReason).HasMaxLength(500);
        b.Property(x => x.Schedule).HasJsonConversion();
        b.HasOne<Discount>().WithMany().HasForeignKey(x => x.DiscountId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstallmentPlan>().WithMany().HasForeignKey(x => x.InstallmentPlanId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.Total);
        b.Ignore(x => x.Collected);
        b.Ignore(x => x.RefundedAmount);
        b.Ignore(x => x.PaidAmount);
        b.Ignore(x => x.Remaining);
        b.Ignore(x => x.PendingRefunds);
        b.Ignore(x => x.Refundable);

        b.OwnsMany(x => x.Payments, p =>
        {
            p.ToTable("payments");
            p.WithOwner().HasForeignKey("InvoiceId");
            p.Property<InvoiceId>("InvoiceId");
            p.HasKey(x => x.Id);
            p.Property(x => x.Id).ValueGeneratedNever();
            p.HasIndex("InvoiceId");
            p.HasIndex(x => x.BankTransactionId);
            p.HasIndex(x => new { x.CollectorId, x.PaidAt });
            p.Property(x => x.Note).HasMaxLength(500);
        });
        b.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Refunds, r =>
        {
            r.ToTable("refunds");
            r.WithOwner().HasForeignKey("InvoiceId");
            r.Property<InvoiceId>("InvoiceId");
            r.HasKey(x => x.Id);
            r.Property(x => x.Id).ValueGeneratedNever();
            r.HasIndex("InvoiceId");
            r.HasIndex(x => x.RefundedAt);
            r.Property(x => x.Reason).HasMaxLength(500);
        });
        b.Navigation(x => x.Refunds).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Requests, q =>
        {
            q.ToTable("invoice_requests");
            q.WithOwner().HasForeignKey("InvoiceId");
            q.Property<InvoiceId>("InvoiceId");
            q.HasKey(x => x.Id);
            q.Property(x => x.Id).ValueGeneratedNever();
            q.HasIndex("InvoiceId");
            q.HasIndex(x => x.Status);
            q.HasIndex(x => x.ApprovalId);
            q.Property(x => x.Reason).HasMaxLength(500);
            q.Property(x => x.DecisionNote).HasMaxLength(500);
            q.Ignore(x => x.IsPending);
        });
        b.Navigation(x => x.Requests).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
