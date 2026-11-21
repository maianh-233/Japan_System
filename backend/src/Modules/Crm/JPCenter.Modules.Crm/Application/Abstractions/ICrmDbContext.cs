using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Abstractions;

/// <summary>DbContext module Crm cho tầng Application (Infrastructure hiện thực bằng CrmDbContext).</summary>
public interface ICrmDbContext
{
    DbSet<Lead> Leads { get; }
    DbSet<LeadRule> LeadRules { get; }
    DbSet<LeadAssignmentLog> LeadAssignmentLogs { get; }
    DbSet<PlacementTest> PlacementTests { get; }
    DbSet<PlacementAttempt> PlacementAttempts { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<Campaign> Campaigns { get; }
    DbSet<MessageTemplate> MessageTemplates { get; }
    DbSet<MessageLog> MessageLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
