using System.Globalization;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp trường hợp chăm sóc (đã giao người phụ trách) + nhật ký liên hệ mẫu từ <c>care.seed.json</c> (HV-14) — sau học viên.
/// Mock không lưu mức rủi ro / dấu hiệu (giao diện tự tính) → nạp mức thấp, chưa có dấu hiệu; job cảnh báo bỏ học tính lại khi chạy.
/// Mỗi nhóm chỉ nạp khi bảng còn trống; HV không có trong DB → bỏ qua.
/// </summary>
internal sealed class CareSeeder(StudentsDbContext db, ILogger<CareSeeder> logger) : IDataSeeder
{
    public int Order => 41;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (!await db.Students.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<CareSeedFile>(typeof(CareSeeder).Assembly, "care.seed.json");
        var students = (await db.Students.IgnoreQueryFilters().Select(s => s.Id).ToListAsync(ct)).ToHashSet();
        StudentId? Student(string mockId) => students.Contains(new StudentId(SeedData.IdOf(mockId))) ? new StudentId(SeedData.IdOf(mockId)) : null;

        int cases = 0, logs = 0;
        if (!await db.CareCases.IgnoreQueryFilters().AnyAsync(ct))
        {
            foreach (var c in data.Cases)
            {
                if (Student(c.StudentId) is not { } sid || c.BranchId is null) continue;
                var x = CareCase.Detect(sid, SeedData.IdOf(c.ClassId), new BranchId(SeedData.IdOf(c.BranchId)), new RiskAssessment(RiskLevel.Low, [], 0),
                    At(c.UpdatedAt));
                x.Assign(c.AssigneeId is null ? null : SeedData.IdOf(c.AssigneeId));
                x.ChangeStatus(SnakeCaseEnumConverter<CareStatus>.Parse(c.Status),
                    c.NextAt is null ? null : DateOnly.Parse(c.NextAt, CultureInfo.InvariantCulture));
                db.Add(x);
                cases++;
            }
        }

        if (!await db.CareLogs.IgnoreQueryFilters().AnyAsync(ct))
        {
            foreach (var l in data.Logs)
            {
                if (Student(l.StudentId) is not { } sid || l.BranchId is null) continue;
                var created = CareLog.Create(sid, SeedData.IdOf(l.ClassId), new BranchId(SeedData.IdOf(l.BranchId)),
                    SnakeCaseEnumConverter<CareChannel>.Parse(l.Channel), l.Note, l.ById is null ? Guid.Empty : SeedData.IdOf(l.ById), l.By, At(l.At));
                if (created.IsFailure)
                {
                    logger.LogWarning("Bỏ qua nhật ký chăm sóc mẫu {Id}: {Error}", l.Id, created.Error.Message);
                    continue;
                }
                db.AddWithId(created.Value, SeedData.IdOf(l.Id));
                logs++;
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã nạp {Cases} trường hợp chăm sóc, {Logs} nhật ký liên hệ mẫu", cases, logs);
    }

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture).ToUniversalTime();

    private sealed record CareSeedFile(List<SeedCase> Cases, List<SeedLog> Logs);

    private sealed record SeedCase(string ClassId, string StudentId, string? BranchId, string? AssigneeId, string Status, string? NextAt, string UpdatedAt);

    private sealed record SeedLog(string Id, string ClassId, string StudentId, string? BranchId, string Channel, string Note, string By, string? ById,
        string At);
}
