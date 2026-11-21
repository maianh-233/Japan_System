using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Questions.GetQuestion;

/// <summary>NH-01.E2 — chi tiết 1 câu (đáp án, giải thích, lời thoại, lịch sử duyệt, thống kê từng đáp án). Ngoài phạm vi → 404.</summary>
public sealed record GetQuestionQuery(Guid Id) : IQuery<QuestionDto>;

public sealed class GetQuestionHandler(QuestionBank bank) : IQueryHandler<GetQuestionQuery, QuestionDto>
{
    public async Task<Result<QuestionDto>> Handle(GetQuestionQuery query, CancellationToken ct)
    {
        var q = await bank.Visible().AsNoTracking().FirstOrDefaultAsync(x => x.Id == new QuestionId(query.Id), ct);
        return q is null ? QuestionErrors.NotFound : await bank.ToDtoAsync(q, ct);
    }
}
