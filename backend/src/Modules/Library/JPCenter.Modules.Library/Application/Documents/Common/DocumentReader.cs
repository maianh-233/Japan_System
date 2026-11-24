using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.Common;

/// <summary>Dựng <see cref="LibraryDocDto"/> (trang nhân viên): tên người tải lên qua Hr.Contracts, lượt 14 ngày, phiên bản <c>xmin</c></summary>
public sealed class DocumentReader(ILibraryDbContext db, DocumentViews views, IEmployeeDirectory employees)
{
    public async Task<IReadOnlyList<LibraryDocDto>> ToDtosAsync(IReadOnlyList<LibraryDocument> docs, CancellationToken ct)
    {
        if (docs.Count == 0) return [];
        var ids = docs.Select(d => d.Id).ToList();
        var versions = await db.Documents.AsNoTracking().Where(d => ids.Contains(d.Id))
            .Select(d => new { d.Id, V = EF.Property<uint>(d, "xmin") }).ToDictionaryAsync(x => x.Id, x => x.V, ct);
        var daily = await views.DailyAsync(ids, ct);
        var uploaders = docs.Select(d => d.UploadedBy).Where(u => u != Guid.Empty).Distinct().ToList();
        var names = uploaders.Count == 0 ? new Dictionary<Guid, string>()
            : (await employees.FindManyAsync(uploaders, ct)).ToDictionary(x => x.Key, x => x.Value.FullName);
        return docs.Select(d => new LibraryDocDto(d.Id.Value, d.Title, d.Subtitle, d.Description, d.Category, d.FileType, d.FileName, d.Size,
                d.Pages, d.Duration, d.Levels.Select(JlptLevelText.Of).ToList(), d.Skills, d.Tags, d.Publisher, d.Year, d.Audience, d.BranchIds.Select(b => b.Value).ToList(),
                d.Watermark, d.AllowDownload, d.Status, names.GetValueOrDefault(d.UploadedBy) ?? "—", d.UploadedBy, d.UploadedAt,
                d.FileType == DocFileType.Link ? d.FileKey : null,
                new LibraryStatsDto(d.ViewCount, d.ReaderCount, d.AvgMinutes, d.LastViewedAt, daily.GetValueOrDefault(d.Id) ?? new int[LibraryStats.Days], []),
                versions.GetValueOrDefault(d.Id)))
            .ToList();
    }

    public async Task<LibraryDocDto> ReloadAsync(LibraryDocumentId id, CancellationToken ct) =>
        (await ToDtosAsync([await db.Documents.AsNoTracking().FirstAsync(d => d.Id == id, ct)], ct))[0];
}
