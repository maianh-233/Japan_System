using JPCenter.Modules.Finance.Domain.Commissions;
using JPCenter.Modules.Finance.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Finance.Infrastructure.Persistence.Configurations;

/// <summary>finance.pay_adjustments — database.md bảng 76. Nhân viên ở schema hr (không FK); tra theo (tháng, nhân viên).</summary>
internal sealed class PayAdjustmentConfiguration : IEntityTypeConfiguration<PayAdjustment>
{
    public void Configure(EntityTypeBuilder<PayAdjustment> b)
    {
        b.ToTable("pay_adjustments");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.Month, x.EmployeeId });
        b.Property(x => x.Month).HasMaxLength(7);
        b.Property(x => x.Amount).HasPrecision(18, 0);
        b.Property(x => x.Reason).HasMaxLength(PayAdjustment.MaxReason);
    }
}

/// <summary>finance.payroll_locks — database.md bảng 77. UQ (kind, month, branch_id); <c>lines</c> là snapshot jsonb.</summary>
internal sealed class PayrollLockConfiguration : IEntityTypeConfiguration<PayrollLock>
{
    public void Configure(EntityTypeBuilder<PayrollLock> b)
    {
        b.ToTable("payroll_locks");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.Kind, x.Month, x.BranchId }).IsUnique();
        b.Property(x => x.Month).HasMaxLength(7);
        b.Property(x => x.Lines).HasColumnType("jsonb");
        b.Ignore(x => x.IsLocked);
        b.Ignore(x => x.IsPaid);
    }
}

/// <summary>finance.commission_tiers — database.md bảng 78. Bảng nhỏ, thay cả bộ mỗi lần sửa.</summary>
internal sealed class CommissionTierConfiguration : IEntityTypeConfiguration<CommissionTier>
{
    public void Configure(EntityTypeBuilder<CommissionTier> b)
    {
        b.ToTable("commission_tiers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Percent).HasPrecision(5, 2);
    }
}

/// <summary>finance.sale_targets — database.md bảng 79. UQ (employee_id, month).</summary>
internal sealed class SaleTargetConfiguration : IEntityTypeConfiguration<SaleTarget>
{
    public void Configure(EntityTypeBuilder<SaleTarget> b)
    {
        b.ToTable("sale_targets");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.EmployeeId, x.Month }).IsUnique();
        b.Property(x => x.Month).HasMaxLength(7);
    }
}
