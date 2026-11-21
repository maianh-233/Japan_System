using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Finance.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Finance.Infrastructure.Persistence.Configurations;

/// <summary>finance.branch_prices — database.md bảng 71. UQ (course_id, branch_id); khóa học / chi nhánh ở schema khác (không FK).</summary>
internal sealed class BranchPriceConfiguration : IEntityTypeConfiguration<BranchPrice>
{
    public void Configure(EntityTypeBuilder<BranchPrice> b)
    {
        b.ToTable("branch_prices");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.CourseId, x.BranchId }).IsUnique();
    }
}

/// <summary>finance.discounts — database.md bảng 72. Mã duy nhất trong số chưa xóa; khóa / chi nhánh lưu uuid[] (schema khác, không FK).</summary>
internal sealed class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> b)
    {
        b.ToTable("discounts");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.Property(x => x.Code).HasMaxLength(15);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Note).HasMaxLength(1000);
    }
}

/// <summary>finance.installment_plans — database.md bảng 73. Các đợt lưu jsonb <c>[{percent, offsetDays}]</c>.</summary>
internal sealed class InstallmentPlanConfiguration : IEntityTypeConfiguration<InstallmentPlan>
{
    public void Configure(EntityTypeBuilder<InstallmentPlan> b)
    {
        b.ToTable("installment_plans");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Parts).HasJsonConversion();
    }
}
