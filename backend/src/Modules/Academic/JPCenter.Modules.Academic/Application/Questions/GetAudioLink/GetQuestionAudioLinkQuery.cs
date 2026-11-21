using JPCenter.Application.Common.Files;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.GetMaterialLink;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Questions.GetAudioLink;

/// <summary>NH-04 — link nghe thử có hạn 15 phút (như tài liệu lộ trình), chỉ nhân viên xem được câu.</summary>
public sealed record GetQuestionAudioLinkQuery(Guid Id) : IQuery<MaterialLinkDto>;

public sealed class GetQuestionAudioLinkHandler(QuestionBank bank, IFileStorage storage, IClock clock) : IQueryHandler<GetQuestionAudioLinkQuery, MaterialLinkDto>
{
    public async Task<Result<MaterialLinkDto>> Handle(GetQuestionAudioLinkQuery query, CancellationToken ct)
    {
        var q = await bank.Visible().AsNoTracking().FirstOrDefaultAsync(x => x.Id == new QuestionId(query.Id), ct);
        if (q?.AudioKey is null) return QuestionErrors.NotFound;
        var url = await storage.GetDownloadUrlAsync(q.AudioKey, new FileLinkOptions(GetMaterialLinkHandler.ValidFor, q.AudioName, Inline: true), ct);
        return new MaterialLinkDto(url.ToString(), clock.UtcNow.Add(GetMaterialLinkHandler.ValidFor));
    }
}
