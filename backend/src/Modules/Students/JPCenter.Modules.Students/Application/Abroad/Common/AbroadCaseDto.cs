using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Application.Abroad.Common;

/// <summary>
/// Hồ sơ du học / XKLĐ — khớp <c>AbroadCase</c> (frontend <c>features/student/abroad/abroad.ts</c>) + mã / tên HV, chi nhánh,
/// <c>AssigneeId</c> (<c>Assignee</c> = tên người phụ trách), Id giấy tờ (đổi trạng thái), <c>RowVersion</c> (sửa thông tin chung).
/// </summary>
public sealed record AbroadCaseDto(
    Guid Id,
    Guid StudentId,
    string StudentCode,
    string StudentName,
    Guid BranchId,
    AbroadProgram Program,
    string Partner,
    string Destination,
    string? Job,
    string Intake,
    string? JlptRequired,
    AbroadStage Stage,
    AbroadCancelledDto? Cancelled,
    IReadOnlyList<AbroadDocDto> Docs,
    AbroadInterviewDto? Interview,
    AbroadCoeDto? Coe,
    AbroadVisaDto? Visa,
    DateOnly? FlightDate,
    AbroadFeeDto Fee,
    Guid AssigneeId,
    string Assignee,
    IReadOnlyList<AbroadNoteDto> Notes,
    DateTimeOffset CreatedAt,
    uint RowVersion);

public sealed record AbroadCancelledDto(string Reason, DateTimeOffset At);

public sealed record AbroadDocDto(Guid Id, string Name, DocumentState State);

public sealed record AbroadInterviewDto(DateOnly Date, InterviewResult? Result);

public sealed record AbroadCoeDto(DateOnly? SubmittedAt, DateOnly? GrantedAt);

public sealed record AbroadVisaDto(DateOnly? AppliedAt, DateOnly? IssuedAt);

public sealed record AbroadFeeDto(decimal Total, decimal Paid);

/// <summary>Ghi chú tiến trình — <c>By</c> = tên người ghi</summary>
public sealed record AbroadNoteDto(DateTimeOffset At, string By, string Text);

/// <summary>Thông tin chung khi tạo / sửa (HV-17 bước 1). <c>FeeTotal</c> VND</summary>
public sealed record AbroadCaseInput(AbroadProgram Program, string? Partner, string? Destination, string? Job, string? Intake, JlptLevel? JlptRequired,
    decimal FeeTotal, Guid AssigneeId);
