using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Library.Domain.TeacherDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Library.Infrastructure.Persistence.Seed;

/// <summary>
/// Dữ liệu mẫu Library (giai đoạn 20) — tài liệu GV từ mock frontend (<c>npm run export-seed</c> → library.seed.json): chỉ nạp khi bảng trống.
/// File mẫu chỉ có khóa trong kho (<c>library/teacher-docs/demo/…</c>) — chưa tải nội dung lên.
/// </summary>
internal sealed class LibrarySeeder(LibraryDbContext db, ILogger<LibrarySeeder> logger) : IDataSeeder
{
    public int Order => 60;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.TeacherDocuments.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<LibrarySeedFile>(typeof(LibrarySeeder).Assembly, "library.seed.json");
        foreach (var d in data.TeacherDocs)
        {
            var kind = SnakeCaseEnumConverter<TeacherDocKind>.Parse(d.Kind);
            var url = d.Url ?? $"library/teacher-docs/demo/{d.FileName}";
            var created = TeacherDocument.Create(SeedData.IdOf(d.TeacherId), d.Title, d.Description, new TeacherDocFile(d.FileName, kind, url, d.Size),
                d.ClassIds.Select(SeedData.IdOf).ToList(), d.SessionId is null ? null : SeedData.IdOf(d.SessionId));
            if (created.IsFailure)
            {
                logger.LogWarning("Bỏ qua tài liệu GV mẫu {Id}: {Error}", d.Id, created.Error.Message);
                continue;
            }
            var doc = created.Value;
            foreach (var studentId in d.ViewedBy) doc.MarkViewed(SeedData.IdOf(studentId), d.CreatedAt.AddHours(2));
            db.AddWithId(doc, SeedData.IdOf(d.Id));
            db.SetCreatedAt(doc, d.CreatedAt);
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã nạp dữ liệu mẫu Library: {Count} tài liệu GV", data.TeacherDocs.Count);
    }

    private sealed record LibrarySeedFile(List<TeacherDocSeed> TeacherDocs);

    private sealed record TeacherDocSeed(string Id, string TeacherId, string Title, string? Description, string FileName, string Kind, long? Size,
        string? Url, List<string> ClassIds, string? SessionId, DateTimeOffset CreatedAt, List<string> ViewedBy);
}
