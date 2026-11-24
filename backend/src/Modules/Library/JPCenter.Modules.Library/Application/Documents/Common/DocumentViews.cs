using JPCenter.Application.Common.Files;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.Common;

/// <summary>
/// TV-05 / TV-06 — ghi lượt xem và cập nhật số liệu tổng hợp trên <c>documents</c> trong CÙNG giao dịch (quy tắc vàng 5).
/// Số liệu ghi bằng <c>ExecuteUpdate</c> (không qua kiểm tra <c>xmin</c>) để 2 người mở cùng lúc không làm nhau lỗi sửa đè.
/// Chặn tải ở server (quy tắc vàng 3): chỉ cấp link ký 10 phút mở inline; link tải (<c>attachment</c>) chỉ khi <c>allowDownload</c>.
/// </summary>
public sealed class DocumentViews(ILibraryDbContext db, IFileStorage storage, IClock clock)
{
    public static readonly TimeSpan LinkValidFor = TimeSpan.FromMinutes(10);

    /// <summary>Mở trình xem: ghi lượt (bắt đầu) → cập nhật số liệu → cấp link + dòng watermark</summary>
    public async Task<DocViewDto> StartAsync(LibraryDocument doc, ViewerIdentity viewer, BranchId? branchId, bool withVersion, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var view = DocumentView.Start(doc.Id, viewer.Type, viewer.Id, viewer.Name, viewer.Role, branchId, now);
        await using (var tx = await Database.BeginTransactionAsync(ct))
        {
            db.DocumentViews.Add(view);
            await db.SaveChangesAsync(ct);
            await RefreshStatsAsync(doc.Id, ct);
            await tx.CommitAsync(ct);
        }

        string url;
        string? download = null;
        DateTimeOffset? expires = null;
        if (doc.FileType == DocFileType.Link) url = doc.FileKey!;
        else
        {
            url = (await storage.GetDownloadUrlAsync(doc.FileKey!, new FileLinkOptions(LinkValidFor, doc.FileName, Inline: true), ct)).ToString();
            if (doc.AllowDownload)
                download = (await storage.GetDownloadUrlAsync(doc.FileKey!, new FileLinkOptions(LinkValidFor, doc.FileName, Inline: false), ct)).ToString();
            expires = now.Add(LinkValidFor);
        }
        var mark = doc.Watermark && doc.FileType != DocFileType.Link ? Watermark.Text(viewer.Name, viewer.Contact, clock.LocalNow) : null;
        uint? version = withVersion ? await VersionAsync(doc.Id, ct) : null;
        return new DocViewDto(view.Id.Value, url, download, expires, mark, doc.AllowDownload && doc.FileType != DocFileType.Link, version);
    }

    /// <summary>Đóng trình xem: lưu phút đọc (0 → 240). Lượt của người khác → không thấy (E3); kết thúc lần 2 → bỏ qua</summary>
    public async Task<Result> EndAsync(Guid viewId, ViewerIdentity viewer, CancellationToken ct)
    {
        var view = await db.DocumentViews.FirstOrDefaultAsync(v => v.Id == new DocumentViewId(viewId), ct);
        if (view is null || !view.BelongsTo(viewer.Type, viewer.Id)) return LibraryDocumentErrors.ViewNotFound;
        if (!view.End(clock.UtcNow)) return Result.Success();
        await using var tx = await Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        await RefreshStatsAsync(view.DocumentId, ct);
        await tx.CommitAsync(ct);
        return Result.Success();
    }

    /// <summary>TV-06 — số liệu đầy đủ (14 ngày theo giờ VN, top 6) tính từ các lượt xem</summary>
    public async Task<LibraryStats> StatsAsync(LibraryDocumentId id, CancellationToken ct)
    {
        var views = await db.DocumentViews.AsNoTracking().Where(v => v.DocumentId == id).ToListAsync(ct);
        return LibraryStats.From(views, clock.Today, Vietnam);
    }

    /// <summary>Lượt xem 14 ngày gần nhất (cũ → mới) của nhiều tài liệu — cột "lượt xem" của danh sách</summary>
    public async Task<IReadOnlyDictionary<LibraryDocumentId, int[]>> DailyAsync(IReadOnlyCollection<LibraryDocumentId> ids, CancellationToken ct)
    {
        var today = clock.Today;
        var from = new DateTimeOffset(today.AddDays(-(LibraryStats.Days - 1)).ToDateTime(TimeOnly.MinValue), Vietnam).ToUniversalTime();
        var rows = await db.DocumentViews.AsNoTracking().Where(v => ids.Contains(v.DocumentId) && v.StartedAt >= from)
            .Select(v => new { v.DocumentId, v.StartedAt }).ToListAsync(ct);
        var result = new Dictionary<LibraryDocumentId, int[]>();
        foreach (var r in rows)
        {
            var back = today.DayNumber - DateOnly.FromDateTime(r.StartedAt.ToOffset(Vietnam).DateTime).DayNumber;
            if (back is < 0 or >= LibraryStats.Days) continue;
            if (!result.TryGetValue(r.DocumentId, out var daily)) result[r.DocumentId] = daily = new int[LibraryStats.Days];
            daily[LibraryStats.Days - 1 - back]++;
        }
        return result;
    }

    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database => ((DbContext)db).Database;

    /// <summary>Lượt, người đọc khác nhau, phút TB (chỉ lượt đã kết thúc — TV-05.E4), lần xem cuối — giống <see cref="LibraryStats.From"/></summary>
    private async Task RefreshStatsAsync(LibraryDocumentId id, CancellationToken ct)
    {
        var q = db.DocumentViews.Where(v => v.DocumentId == id);
        var views = await q.CountAsync(ct);
        var readers = await q.Select(v => new { v.ViewerType, v.ViewerId }).Distinct().CountAsync(ct);
        var avg = decimal.Round(await q.Where(v => v.EndedAt != null).Select(v => (decimal?)v.Minutes).AverageAsync(ct) ?? 0, 1);
        var last = await q.MaxAsync(v => (DateTimeOffset?)v.StartedAt, ct);
        await db.Documents.Where(d => d.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.ViewCount, views)
            .SetProperty(d => d.ReaderCount, readers)
            .SetProperty(d => d.AvgMinutes, avg)
            .SetProperty(d => d.LastViewedAt, last), ct);
    }

    private Task<uint> VersionAsync(LibraryDocumentId id, CancellationToken ct) =>
        db.Documents.AsNoTracking().Where(d => d.Id == id).Select(d => EF.Property<uint>(d, "xmin")).FirstAsync(ct);
}
