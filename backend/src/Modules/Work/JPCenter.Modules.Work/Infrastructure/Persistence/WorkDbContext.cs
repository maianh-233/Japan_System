using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.Modules.Work.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Infrastructure.Persistence;

/// <summary>
/// DbContext module Work — schema <c>work</c>: task / Kanban, xếp ca, đăng ký rảnh / bận, bàn giao ca (user story 14, giai đoạn 21).
/// </summary>
public sealed class WorkDbContext(DbContextOptions<WorkDbContext> options) : ModuleDbContextBase(options), IWorkDbContext
{
    public const string SchemaName = "work";

    protected override string Schema => SchemaName;

    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<ShiftRegistration> ShiftRegistrations => Set<ShiftRegistration>();
    public DbSet<Handover> Handovers => Set<Handover>();
}
