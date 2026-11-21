using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence;

/// <summary>
/// DbContext module Crm — schema <c>crm</c>: lead (+ lịch sử tư vấn, liên kết học viên), quy tắc chia lead + nhật ký, test xếp lớp + lượt làm,
/// lịch hẹn, chiến dịch, mẫu tin + nhật ký tin (user story 05).
/// </summary>
public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : ModuleDbContextBase(options), ICrmDbContext
{
    public const string SchemaName = "crm";

    protected override string Schema => SchemaName;

    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadRule> LeadRules => Set<LeadRule>();
    public DbSet<LeadAssignmentLog> LeadAssignmentLogs => Set<LeadAssignmentLog>();
    public DbSet<PlacementTest> PlacementTests => Set<PlacementTest>();
    public DbSet<PlacementAttempt> PlacementAttempts => Set<PlacementAttempt>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();
}
