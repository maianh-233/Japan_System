using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Permissions.ExpireOverrides;
using JPCenter.Modules.System.Application.Trash.PurgeExpiredTrash;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>
/// Job hằng ngày 02:00 giờ Việt Nam (Quartz):
/// 1. PQ-12 — báo quyền lẻ sắp hết hạn, ghi nhật ký quyền lẻ đã hết hạn (<see cref="ExpireOverridesCommand"/>).
/// 2. TR-04 — xóa hẳn mục Thùng rác quá hạn (<see cref="PurgeExpiredTrashCommand"/>).
/// 3. Dọn dữ liệu tạm: Idempotency-Key quá hạn; token dùng 1 lần (OTP, link mời) hết hạn quá 30 ngày;
///    phiên đăng nhập đã kết thúc quá 90 ngày (giữ đủ cho "Lịch sử đăng nhập"); nhật ký tích hợp cũ hơn 180 ngày (TH-05).
/// </summary>
[DisallowConcurrentExecution]
public sealed class SystemHousekeepingJob(ISender sender, SystemDbContext db, IClock clock, ILogger<SystemHousekeepingJob> logger) : IJob
{
    public const string Cron = "0 0 2 * * ?";
    public static readonly TimeSpan KeepTokens = TimeSpan.FromDays(30);
    public static readonly TimeSpan KeepSessions = TimeSpan.FromDays(90);

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var overrides = await sender.Send(new ExpireOverridesCommand(), ct);
        var trash = await sender.Send(new PurgeExpiredTrashCommand(), ct);
        var now = clock.UtcNow;
        var keys = await db.IdempotencyKeys.Where(x => x.ExpiresAt < now).ExecuteDeleteAsync(ct);
        var tokens = await db.OneTimeTokens.Where(x => x.ExpiresAt < now - KeepTokens).ExecuteDeleteAsync(ct);
        var sessions = await db.UserSessions
            .Where(x => (x.RevokedAt != null && x.RevokedAt < now - KeepSessions) || x.ExpiresAt < now - KeepSessions)
            .ExecuteDeleteAsync(ct);
        var integrationLogs = await db.IntegrationLogs.Where(x => x.At < now.AddDays(-IntegrationLog.RetentionDays)).ExecuteDeleteAsync(ct);

        logger.LogInformation(
            "Dọn hệ thống: nhắc {Reminded} quyền lẻ sắp hết hạn, {Expired} quyền lẻ hết hạn; xóa hẳn {Trash} mục thùng rác; xóa {Keys} idempotency key, {Tokens} token, {Sessions} phiên, {IntegrationLogs} nhật ký tích hợp",
            overrides.IsSuccess ? overrides.Value.Reminded : 0, overrides.IsSuccess ? overrides.Value.Expired : 0, trash.IsSuccess ? trash.Value : 0,
            keys, tokens, sessions, integrationLogs);
    }
}
