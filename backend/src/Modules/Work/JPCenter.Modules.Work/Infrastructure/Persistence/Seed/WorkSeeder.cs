using System.Globalization;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Work.Infrastructure.Persistence.Seed;

/// <summary>
/// Dữ liệu mẫu Work (giai đoạn 21, user story 14) từ mock frontend (<c>npm run export-seed</c> → work.seed.json): lịch ca 4 tuần, đăng ký rảnh / bận,
/// task, bàn giao ca. Mỗi nhóm chỉ nạp khi bảng của nhóm đó còn trống. Ngày tính theo ngày xuất file.
/// </summary>
internal sealed class WorkSeeder(WorkDbContext db, ILogger<WorkSeeder> logger) : IDataSeeder
{
    public int Order => 70;

    public async Task SeedAsync(CancellationToken ct)
    {
        var data = SeedData.Read<WorkSeedFile>(typeof(WorkSeeder).Assembly, "work.seed.json");
        var counts = new List<string>();

        if (!await db.ShiftAssignments.AnyAsync(ct))
        {
            foreach (var s in data.Shifts)
            {
                var date = Date(s.Date);
                // "Hôm nay" của mẫu = ngày ca (ca đã qua vẫn nạp được)
                var shift = ShiftAssignment.Create(SeedData.IdOf(s.EmployeeId), Branch(s.BranchId), date, Parse<ShiftKey>(s.Shift), date, busy: false).Value;
                db.AddWithId(shift, SeedData.IdOf(s.Id));
            }
            counts.Add($"{data.Shifts.Count} ca");
        }

        if (!await db.ShiftRegistrations.AnyAsync(ct))
        {
            var regs = data.Registrations.GroupBy(r => (r.EmployeeId, r.Date, r.Shift)).Select(g => g.Last()).ToList();
            foreach (var r in regs)
            {
                var date = Date(r.Date);
                db.ShiftRegistrations.Add(ShiftRegistration.Create(SeedData.IdOf(r.EmployeeId), date, Parse<ShiftKey>(r.Shift),
                    Parse<ShiftRegistrationKind>(r.Status), data.GeneratedAt, date).Value);
            }
            counts.Add($"{regs.Count} đăng ký rảnh / bận");
        }

        if (!await db.Tasks.IgnoreQueryFilters().AnyAsync(ct))
        {
            foreach (var t in data.Tasks)
            {
                var created = WorkTask.Create(t.Code, t.Title, t.Description, Branch(t.BranchId), SeedData.IdOf(t.AssigneeId), SeedData.IdOf(t.CreatorId),
                    t.WatcherIds.Select(SeedData.IdOf), Parse<TaskPriority>(t.Priority), t.Due is null ? null : Date(t.Due), Parse<TaskRepeat>(t.Repeat),
                    t.Checklist.Select(c => c.Text));
                if (created.IsFailure)
                {
                    logger.LogWarning("Bỏ qua task mẫu {Id}: {Error}", t.Id, created.Error.Message);
                    continue;
                }
                var task = created.Value;
                for (var i = 0; i < t.Checklist.Count; i++)
                    if (t.Checklist[i].Done) task.TickChecklist(task.Checklist[i].Id, true);
                if (t.Link is { } l) task.LinkTo(Parse<TaskLinkType>(l.Type), SeedData.IdOf(l.Id), l.Label);
                foreach (var c in t.Comments) task.AddComment(c.Text, c.ById is null ? Guid.Empty : SeedData.IdOf(c.ById), c.By, c.At);
                var status = Parse<WorkTaskStatus>(t.Status);
                // Task mẫu đã xong không sinh lần lặp (mẫu đã có sẵn)
                if (status != WorkTaskStatus.Todo) task.Move(status, t.CompletedAt ?? t.CreatedAt, DateOnly.FromDateTime(t.CreatedAt.Date), () => t.Code + "-R");
                db.AddWithId(task, SeedData.IdOf(t.Id));
                db.SetCreatedAt(task, t.CreatedAt);
            }
            counts.Add($"{data.Tasks.Count} task");
        }

        if (!await db.Handovers.AnyAsync(ct))
        {
            foreach (var h in data.Handovers)
            {
                var created = Handover.Create(Branch(h.BranchId), Date(h.Date), Parse<ShiftKey>(h.FromShift), SeedData.IdOf(h.FromId), SeedData.IdOf(h.ToId),
                    Money.FromTrusted(h.Cash.Expected), Money.FromTrusted(h.Cash.Counted), h.Cash.Note, h.Issues,
                    h.Leads.Select(l => (SeedData.IdOf(l.LeadId), l.Note)), h.Pending.Select(p => p.Text), h.CreatedAt);
                if (created.IsFailure)
                {
                    logger.LogWarning("Bỏ qua bàn giao mẫu {Id}: {Error}", h.Id, created.Error.Message);
                    continue;
                }
                var handover = created.Value;
                if (h.Status != "pending" && h.Cash.ReceivedCount is { } count)
                {
                    handover.Receive(handover.ToId, Money.FromTrusted(count), h.ReceiverNote, h.ReceivedAt ?? h.CreatedAt);
                    var texts = h.Pending.Select(p => p.Text.Trim()).ToList();
                    foreach (var item in handover.PendingItems.ToList())
                        if (h.Pending.ElementAtOrDefault(texts.IndexOf(item.Text))?.Done == true) handover.TickItem(handover.ToId, item.Id, true);
                }
                db.AddWithId(handover, SeedData.IdOf(h.Id));
                db.SetCreatedAt(handover, h.CreatedAt);
            }
            counts.Add($"{data.Handovers.Count} bàn giao ca");
        }

        if (counts.Count == 0) return;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã nạp dữ liệu mẫu Work: {Counts}", string.Join(", ", counts));
    }

    private static DateOnly Date(string s) => DateOnly.Parse(s, CultureInfo.InvariantCulture);
    private static BranchId Branch(string? mockId) => new(SeedData.IdOf(mockId ?? "cg"));
    private static T Parse<T>(string value) where T : struct, Enum => SnakeCaseEnumConverter<T>.Parse(value);

    private sealed record WorkSeedFile(DateTimeOffset GeneratedAt, List<ShiftSeed> Shifts, List<RegistrationSeed> Registrations, List<TaskSeed> Tasks,
        List<HandoverSeed> Handovers);

    private sealed record ShiftSeed(string Id, string EmployeeId, string? BranchId, string Date, string Shift);

    private sealed record RegistrationSeed(string EmployeeId, string Date, string Shift, string Status);

    private sealed record TaskSeed(string Id, string Code, string Title, string? Description, string AssigneeId, string CreatorId, List<string> WatcherIds,
        string Priority, string Status, string? Due, string Repeat, LinkSeed? Link, List<ChecklistSeed> Checklist, List<CommentSeed> Comments,
        DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, string? BranchId);

    private sealed record LinkSeed(string Type, string Id, string Label);

    private sealed record ChecklistSeed(string Text, bool Done);

    private sealed record CommentSeed(DateTimeOffset At, string By, string? ById, string Text);

    private sealed record HandoverSeed(string Id, string BranchId, string Date, string FromShift, string FromId, string ToId, CashSeed Cash,
        List<HandoverLeadSeed> Leads, List<PendingSeed> Pending, string? Issues, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ReceivedAt,
        string? ReceiverNote);

    private sealed record CashSeed(decimal Expected, decimal Counted, decimal? ReceivedCount, string? Note);

    private sealed record HandoverLeadSeed(string LeadId, string Note);

    private sealed record PendingSeed(string Text, bool? Done);
}
