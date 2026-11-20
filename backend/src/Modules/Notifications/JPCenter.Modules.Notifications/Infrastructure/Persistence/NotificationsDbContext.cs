using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Notifications.Application.Abstractions;
using JPCenter.Modules.Notifications.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Notifications.Infrastructure.Persistence;

/// <summary>DbContext module Notifications — schema <c>notifications</c>: chuông thông báo trong app (user story 10, TB-xx).</summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : ModuleDbContextBase(options), INotificationsDbContext
{
    public const string SchemaName = "notifications";

    protected override string Schema => SchemaName;

    public DbSet<Notification> Notifications => Set<Notification>();
}
