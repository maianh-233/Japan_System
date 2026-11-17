using JPCenter.Application.Common.Files;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Curricula.GetMaterialLink;

/// <summary>
/// LT-07 — link mở tài liệu cho nhân viên (trang Lộ trình). Tài liệu file → link tạm có hạn từ kho (xem trên trình duyệt hoặc tải về
/// đúng tên gốc); tài liệu link → đường dẫn gốc. ⏳ Cổng GV / HV dùng endpoint riêng khi có Lớp học (kiểm GV dạy / HV đang học).
/// </summary>
public sealed record GetMaterialLinkQuery(Guid CurriculumId, Guid MaterialId, bool Download) : IQuery<MaterialLinkDto>;

/// <param name="ExpiresAt">null = link ngoài, không hết hạn</param>
public sealed record MaterialLinkDto(string Url, DateTimeOffset? ExpiresAt);

public sealed class GetMaterialLinkHandler(IAcademicDbContext db, IFileStorage storage, IClock clock)
    : IQueryHandler<GetMaterialLinkQuery, MaterialLinkDto>
{
    public static readonly TimeSpan ValidFor = TimeSpan.FromMinutes(15);

    public async Task<Result<MaterialLinkDto>> Handle(GetMaterialLinkQuery query, CancellationToken ct)
    {
        var c = await db.Curricula.AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(x => x.Id == new CurriculumId(query.CurriculumId), ct);
        var m = c?.FindMaterial(new MaterialId(query.MaterialId));
        if (m is null) return CurriculumErrors.MaterialNotFound;
        if (!m.IsFile) return new MaterialLinkDto(m.Url!, null);

        var url = await storage.GetDownloadUrlAsync(m.FileKey!, new FileLinkOptions(ValidFor, m.FileName, Inline: !query.Download), ct);
        return new MaterialLinkDto(url.ToString(), clock.UtcNow.Add(ValidFor));
    }
}
