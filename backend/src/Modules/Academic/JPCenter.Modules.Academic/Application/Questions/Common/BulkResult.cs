namespace JPCenter.Modules.Academic.Application.Questions.Common;

/// <summary>Kết quả thao tác hàng loạt (NH-05, NH-06): câu làm được + câu bỏ qua kèm lý do (không làm hỏng cả lô)</summary>
public sealed record BulkResult(IReadOnlyList<QuestionDto> Done, IReadOnlyList<BulkSkipped> Skipped);

public sealed record BulkSkipped(Guid Id, string? Code, string Reason);
