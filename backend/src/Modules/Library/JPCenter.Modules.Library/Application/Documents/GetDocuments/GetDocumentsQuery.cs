using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Application.Common.Text;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Documents.GetDocuments;

/// <summary>
/// TV-01 — Thư viện › Thư viện tài liệu: lọc chi nhánh, nhóm, cấp độ, kỹ năng, dạng tệp, đối tượng, từ khóa (tên, phụ đề, thẻ, NXB — không dấu);
/// tài liệu ẩn chỉ hiện khi <paramref name="IncludeHidden"/> (E2); phạm vi <c>branch</c> không thấy tài liệu chỉ của chi nhánh khác (E1). Mới nhất trước.
/// </summary>
public sealed record GetDocumentsQuery(Guid? BranchId, DocCategory? Category, JlptLevel? Level, JapaneseSkill? Skill, DocFileType? FileType,
    DocAudience? Audience, string? Q, bool IncludeHidden) : IQuery<IReadOnlyList<LibraryDocDto>>;

public sealed class GetDocumentsHandler(ILibraryDbContext db, LibraryAccess access, DocumentReader reader)
    : IQueryHandler<GetDocumentsQuery, IReadOnlyList<LibraryDocDto>>
{
    public async Task<Result<IReadOnlyList<LibraryDocDto>>> Handle(GetDocumentsQuery q, CancellationToken ct)
    {
        var query = db.Documents.AsNoTracking();
        if (!q.IncludeHidden) query = query.Where(d => d.Status == DocStatus.Published);
        if (q.Category is { } cat) query = query.Where(d => d.Category == cat);
        if (q.FileType is { } ft) query = query.Where(d => d.FileType == ft);
        // Thư viện nhỏ (vài trăm tài liệu) — lọc mảng (cấp độ, kỹ năng, chi nhánh) và từ khóa trong bộ nhớ
        var docs = (await query.ToListAsync(ct)).Where(d => access.Covers(d, PermissionAction.View));
        if (q.BranchId is { } b) docs = docs.Where(d => d.IsForBranches([new BranchId(b)]));
        if (q.Level is { } level) docs = docs.Where(d => d.Levels.Contains(level));
        if (q.Skill is { } skill) docs = docs.Where(d => d.Skills.Contains(skill));
        if (q.Audience is { } aud) docs = docs.Where(d => d.Audience.Contains(aud));
        if (TextSearch.Normalize(q.Q) is { Length: > 0 } term)
            docs = docs.Where(d => new[] { d.Title, d.Subtitle, d.Publisher }.Concat(d.Tags).Any(t => TextSearch.Normalize(t).Contains(term)));
        return Result.Success(await reader.ToDtosAsync(docs.OrderByDescending(d => d.UploadedAt).ToList(), ct));
    }
}
