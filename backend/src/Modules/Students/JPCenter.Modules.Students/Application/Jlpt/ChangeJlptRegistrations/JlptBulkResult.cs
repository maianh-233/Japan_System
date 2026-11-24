using JPCenter.Modules.Students.Application.Jlpt.Common;

namespace JPCenter.Modules.Students.Application.Jlpt.ChangeJlptRegistrations;

/// <summary>Kết quả thao tác hàng loạt: lượt đã cập nhật + lượt bỏ qua (mã lỗi, lý do)</summary>
public sealed record JlptBulkResult(IReadOnlyList<JlptRegistrationDto> Updated, IReadOnlyList<JlptBulkSkipped> Skipped);

public sealed record JlptBulkSkipped(Guid Id, string Code, string Message);
