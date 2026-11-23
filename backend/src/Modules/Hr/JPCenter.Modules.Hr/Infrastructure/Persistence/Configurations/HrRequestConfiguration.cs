using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.HrRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Configurations;

/// <summary>
/// hr.hr_requests + hr_request_steps — database.md bảng 83, 84. Ca đổi (swap_*_shift_id → work.shift_assignments) không FK (schema khác);
/// người duyệt trong bước không FK (bước mở, người đã nghỉ vẫn giữ lịch sử).
/// </summary>
internal sealed class HrRequestConfiguration : IEntityTypeConfiguration<HrRequest>
{
    public void Configure(EntityTypeBuilder<HrRequest> b)
    {
        b.ToTable("hr_requests");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.EmployeeId, x.Status });
        b.HasIndex(x => new { x.BranchId, x.Status });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.SwapWithEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.Ignore(x => x.CurrentStep);
        b.Ignore(x => x.DeductsLeave);
        b.Ignore(x => x.BlocksDays);

        b.OwnsMany(x => x.Steps, s =>
        {
            s.ToTable("hr_request_steps");
            s.WithOwner().HasForeignKey("HrRequestId");
            s.Property<HrRequestId>("HrRequestId");
            s.HasKey("HrRequestId", nameof(ApprovalStep.Order));
            s.Property(x => x.Order).ValueGeneratedNever();
            s.HasIndex(x => new { x.ApproverId, x.Decision });
            s.Property(x => x.Role).HasMaxLength(100);
            s.Property(x => x.ApproverName).HasMaxLength(200);
            s.Property(x => x.DecidedByName).HasMaxLength(200);
            s.Property(x => x.Comment).HasMaxLength(1000);
        });
        b.Navigation(x => x.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
