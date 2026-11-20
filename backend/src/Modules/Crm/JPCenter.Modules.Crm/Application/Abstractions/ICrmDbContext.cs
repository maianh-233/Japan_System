using JPCenter.Modules.Crm.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Abstractions;

/// <summary>DbContext module Crm cho tầng Application (Infrastructure hiện thực bằng CrmDbContext).</summary>
public interface ICrmDbContext
{
    DbSet<Lead> Leads { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
