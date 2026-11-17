using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Configurations;

/// <summary>hr.employees (+ hr.teacher_profiles) — database.md bảng 80, 81.</summary>
internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("employees");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => x.BranchId);
        b.Property(x => x.LeaveBalance).HasPrecision(5, 1);

        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);

        b.ComplexProperty(x => x.Contract, c =>
        {
            c.Property(p => p.Type).HasColumnName("contract_type");
            c.Property(p => p.No).HasColumnName("contract_no");
            c.Property(p => p.Start).HasColumnName("contract_start");
            c.Property(p => p.End).HasColumnName("contract_end");
            c.Property(p => p.BaseSalary).HasColumnName("contract_base_salary");
            c.Property(p => p.Allowance).HasColumnName("contract_allowance");
        });

        b.Property(x => x.QuitReason).HasMaxLength(500);

        // Lịch sử hợp đồng (NS-06.E5)
        b.OwnsMany(x => x.ContractHistory, h =>
        {
            h.ToTable("employment_contracts");
            h.WithOwner().HasForeignKey("EmployeeId");
            h.Property<EmployeeId>("EmployeeId");
            h.HasKey(x => x.Id);
            h.Property(x => x.Id).ValueGeneratedNever();
            h.Property(x => x.No).HasMaxLength(50);
            h.HasIndex("EmployeeId");
        });
        b.Navigation(x => x.ContractHistory).HasField("_contractHistory").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsOne(x => x.Teacher, t =>
        {
            t.ToTable("teacher_profiles");
            t.WithOwner().HasForeignKey("EmployeeId");
            t.Property<EmployeeId>("EmployeeId");
            t.HasKey("EmployeeId");
            t.Property(p => p.Availability).HasJsonConversion();
        });
    }
}
