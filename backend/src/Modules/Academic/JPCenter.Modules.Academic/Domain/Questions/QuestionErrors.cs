using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Questions;

public static class QuestionErrors
{
    public static readonly Error InvalidStatus = Error.Conflict("academic.question.invalid_status", "Không chuyển được trạng thái câu hỏi");
}
