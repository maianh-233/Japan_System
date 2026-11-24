using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Text;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.GetPortalLibrary;

/// <summary>
/// TV-07 (cổng HV / PH — <paramref name="StudentId"/> = con PH chọn) · TV-08 (cổng GV): tài liệu đang hiển thị, đúng đối tượng + chi nhánh;
/// trùng cấp độ lớp đang học / dạy lên trước, rồi mới nhất. Lọc nhóm, từ khóa. Không trả thông tin nội bộ (TV-07.E2).
/// </summary>
public sealed record GetPortalLibraryQuery(Guid? StudentId, DocCategory? Category, string? Q) : IQuery<IReadOnlyList<PortalLibraryDocDto>>;

public sealed class GetPortalLibraryHandler(ILibraryDbContext db, PortalLibrary portal)
    : IQueryHandler<GetPortalLibraryQuery, IReadOnlyList<PortalLibraryDocDto>>
{
    public async Task<Result<IReadOnlyList<PortalLibraryDocDto>>> Handle(GetPortalLibraryQuery q, CancellationToken ct)
    {
        var scope = await portal.ScopeAsync(q.StudentId, ct);
        if (scope.IsFailure) return scope.Error;
        var s = scope.Value;
        var query = db.Documents.AsNoTracking().Where(d => d.Status == DocStatus.Published);
        if (q.Category is { } cat) query = query.Where(d => d.Category == cat);
        IEnumerable<LibraryDocument> docs = (await query.ToListAsync(ct)).Where(s.Sees);
        if (TextSearch.Normalize(q.Q) is { Length: > 0 } term)
            docs = docs.Where(d => new[] { d.Title, d.Subtitle, d.Publisher }.Concat(d.Tags).Any(t => TextSearch.Normalize(t).Contains(term)));
        return docs.Select(d => (Doc: d, Match: d.Levels.Any(s.Levels.Contains)))
            .OrderByDescending(x => x.Match).ThenByDescending(x => x.Doc.UploadedAt)
            .Select(x => new PortalLibraryDocDto(x.Doc.Id.Value, x.Doc.Title, x.Doc.Subtitle, x.Doc.Description, x.Doc.Category, x.Doc.FileType,
                x.Doc.FileName, x.Doc.Size, x.Doc.Pages, x.Doc.Duration, x.Doc.Levels.Select(JlptLevelText.Of).ToList(), x.Doc.Skills, x.Doc.Tags, x.Doc.Publisher, x.Doc.Year,
                x.Doc.Watermark, x.Doc.AllowDownload, x.Doc.UploadedAt, x.Match))
            .ToList();
    }
}
