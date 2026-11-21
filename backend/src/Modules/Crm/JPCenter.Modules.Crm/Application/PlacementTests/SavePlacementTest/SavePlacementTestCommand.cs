using System.Globalization;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.PlacementTests.SavePlacementTest;

/// <summary>
/// CRM-12 — soạn / sửa bài test. Thêm mới: bốc câu đã duyệt theo từng phần (thiếu → E2), danh sách câu cố định.
/// Sửa: tên / mô tả / thang luôn được; đổi phần / thời gian hoặc <paramref name="Redraw"/> ("Đổi đề") → bốc lại — đã có người làm thì từ chối (E6).
/// </summary>
public sealed record SavePlacementTestCommand(Guid? Id, string? Name, string? Description, List<SectionDto> Sections, int DurationMin,
    List<BandDto> Bands, bool Redraw, uint Version) : ICommand<PlacementTestDto>;

public sealed class SavePlacementTestHandler(ICrmDbContext db, CrmAccess access, PlacementBank bank, PlacementReader reader, IAuditTrail audit)
    : ICommandHandler<SavePlacementTestCommand, PlacementTestDto>
{
    public async Task<Result<PlacementTestDto>> Handle(SavePlacementTestCommand cmd, CancellationToken ct)
    {
        var sections = new List<PlacementSection>();
        foreach (var s in cmd.Sections ?? [])
        {
            var level = PlacementBank.ParseLevel(s.Level);
            if (level.IsFailure) return level.Error;
            sections.Add(new PlacementSection(level.Value, s.Skill, s.Count));
        }
        if (sections.Any(s => s.Count <= 0)) return PlacementTestErrors.InvalidSection;
        var bands = (cmd.Bands ?? []).Select(b => new PlacementBand(b.MinPct, b.Level ?? "", b.Course ?? "")).ToList();

        PlacementTest test;
        if (cmd.Id is { } id)
        {
            var found = await db.PlacementTests.FirstOrDefaultAsync(t => t.Id == new PlacementTestId(id), ct);
            if (found is null) return PlacementTestErrors.NotFound;
            if (cmd.Version != 0) ((DbContext)db).Entry(found).Property("xmin").OriginalValue = cmd.Version;
            if (found.UpdateInfo(cmd.Name ?? "", cmd.Description, bands) is { IsFailure: true } info) return info.Error;
            if (cmd.Redraw || !sections.SequenceEqual(found.Sections) || cmd.DurationMin != found.DurationMinutes)
            {
                var hasAttempts = await db.PlacementAttempts.AnyAsync(a => a.TestId == found.Id, ct);
                if (hasAttempts) return PlacementTestErrors.HasAttempts;
                var keepQuestions = !cmd.Redraw && sections.SequenceEqual(found.Sections);
                var questions = keepQuestions ? found.QuestionIds : await DrawOrFail(sections, ct);
                if (questions is null) return _drawError!;
                if (found.Restructure(sections, cmd.DurationMin, questions, hasAttempts) is { IsFailure: true } bad) return bad.Error;
            }
            test = found;
        }
        else
        {
            if (sections.Count == 0) return PlacementTestErrors.NoSection;
            var questions = await DrawOrFail(sections, ct);
            if (questions is null) return _drawError!;
            var created = PlacementTest.Create(await NextCodeAsync(ct), cmd.Name ?? "", cmd.Description, sections, cmd.DurationMin, bands, questions,
                access.ActorId);
            if (created.IsFailure) return created.Error;
            test = created.Value;
            db.PlacementTests.Add(test);
        }

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.Id is null ? "Soạn bài test xếp lớp" : "Sửa bài test xếp lớp", $"{test.Code} — {test.Name}");
        return await reader.TestAsync(test, ct);
    }

    private Error? _drawError;

    private async Task<List<Guid>?> DrawOrFail(IReadOnlyList<PlacementSection> sections, CancellationToken ct)
    {
        var drawn = await bank.DrawAsync(sections, ct);
        if (drawn.IsSuccess) return drawn.Value;
        _drawError = drawn.Error;
        return null;
    }

    /// <summary>Mã bài test "PT-0004" (bài mẫu có mã chữ giữ nguyên)</summary>
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var codes = await db.PlacementTests.IgnoreQueryFilters().Select(t => t.Code).ToListAsync(ct);
        var max = codes.Select(c => c.StartsWith("PT-", StringComparison.Ordinal)
            && int.TryParse(c[3..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"PT-{max + 1:D4}";
    }
}
