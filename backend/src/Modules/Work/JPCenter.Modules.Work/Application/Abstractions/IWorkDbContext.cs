using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.Modules.Work.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Abstractions;

/// <summary>DbContext module Work cho tầng Application (Infrastructure hiện thực bằng WorkDbContext).</summary>
public interface IWorkDbContext
{
    DbSet<WorkTask> Tasks { get; }
    DbSet<ShiftAssignment> ShiftAssignments { get; }
    DbSet<ShiftRegistration> ShiftRegistrations { get; }
    DbSet<Handover> Handovers { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
