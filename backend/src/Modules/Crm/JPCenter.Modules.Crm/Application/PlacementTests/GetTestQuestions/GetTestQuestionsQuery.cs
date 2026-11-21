using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.PlacementTests.GetTestQuestions;

/// <summary>Câu trong đề (nhân viên xem trước) — có đáp án đúng; câu đã ngừng dùng vẫn hiện (đề cố định — CRM-12.E5)</summary>
public sealed record StaffQuestionDto(Guid Id, string Code, string Level, string Skill, string Format, string Stem, string? Passage,
    IReadOnlyList<string> Options, int? Answer);

public sealed record GetTestQuestionsQuery(Guid TestId) : IQuery<IReadOnlyList<StaffQuestionDto>>;

public sealed class GetTestQuestionsHandler(ICrmDbContext db, IQuestionPicker picker) : IQueryHandler<GetTestQuestionsQuery, IReadOnlyList<StaffQuestionDto>>
{
    public async Task<Result<IReadOnlyList<StaffQuestionDto>>> Handle(GetTestQuestionsQuery query, CancellationToken ct)
    {
        var test = await db.PlacementTests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == new PlacementTestId(query.TestId), ct);
        if (test is null) return PlacementTestErrors.NotFound;
        var questions = await picker.GetAsync(test.QuestionIds, ct);
        var answers = await picker.AnswersAsync(test.QuestionIds, ct);
        return questions.Select(q => new StaffQuestionDto(q.Id, q.Code, q.Level, q.Skill, q.Format, q.Stem, q.Passage, q.Options,
            answers.TryGetValue(q.Id, out var a) ? a : null)).ToList();
    }
}
