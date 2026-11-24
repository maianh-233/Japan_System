using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Application.Jlpt.Common;

/// <summary>
/// Kỳ thi JLPT — khớp <c>JlptSession</c> (frontend <c>features/student/jlpt/jlpt.ts</c>) + lệ phí theo cấp độ ("N5" → "N1") và địa điểm
/// (trước ở hằng số <c>JLPT_FEE</c>, <c>JLPT_VENUES</c>). <c>Closed</c> = đã quá hạn nhận hồ sơ (HV-16.E6 — chỉ cảnh báo).
/// </summary>
public sealed record JlptSessionDto(Guid Id, string Label, DateOnly ExamDate, DateOnly RegClose, DateOnly ResultDate,
    IReadOnlyDictionary<string, decimal> Fees, IReadOnlyList<string> Venues, bool Closed);

/// <summary>Checklist hồ sơ — khớp <c>docs: Record&lt;'photo' | 'idCard' | 'form', boolean&gt;</c></summary>
public sealed record JlptDocsDto(bool Photo, bool IdCard, bool Form);

/// <summary>Chứng chỉ gốc — <c>ReceivedBy</c> / <c>HandedBy</c> là tên nhân viên</summary>
public sealed record JlptCertificateDto(string No, DateTimeOffset ReceivedAt, string ReceivedBy, DateTimeOffset? HandedAt, string? HandedBy, string? HandedTo);

/// <summary>
/// Lượt đăng ký thi — khớp <c>JlptReg</c> (+ mã / tên HV, lệ phí, kết quả chấm server tính: <c>Total</c>, <c>Passed</c>, <c>FailSections</c>).
/// <c>Scores</c> null khi chưa có điểm.
/// </summary>
public sealed record JlptRegistrationDto(
    Guid Id,
    Guid StudentId,
    string StudentCode,
    string StudentName,
    Guid BranchId,
    Guid SessionId,
    string Level,
    string Venue,
    JlptRegistrationStatus Status,
    JlptDocsDto Docs,
    decimal Fee,
    DateTimeOffset? PaidAt,
    string? AdmitNo,
    IReadOnlyList<int>? Scores,
    int? Total,
    bool? Passed,
    IReadOnlyList<string> FailSections,
    JlptCertificateDto? Certificate,
    string? Note,
    DateTimeOffset CreatedAt);

/// <summary>Đăng ký nhiều HV 1 lần: lượt tạo được, HV bỏ qua (đã đăng ký kỳ này / không trong phạm vi) + cảnh báo (quá hạn nhận hồ sơ)</summary>
public sealed record JlptRegisterResult(IReadOnlyList<JlptRegistrationDto> Created, IReadOnlyList<JlptSkipped> Skipped, IReadOnlyList<string> Warnings);

public sealed record JlptSkipped(Guid StudentId, string? StudentCode, string Code, string Message);

/// <summary>HV-16 bước 4 — tỉ lệ đỗ 1 nhóm (cấp độ / lớp): đã thi (có kết quả), vắng, đỗ, tỉ lệ đỗ % trên số đã thi</summary>
public sealed record JlptPassRate(string Key, string Label, int Registered, int Sat, int Absent, int Passed, int? PassRate);

/// <summary>Thống kê tỉ lệ đỗ theo cấp độ và theo lớp (lớp HV đang học lúc thi). ⏳ Theo GV: cần Academic.Contracts trả GV của lớp</summary>
public sealed record JlptStatsDto(IReadOnlyList<JlptPassRate> ByLevel, IReadOnlyList<JlptPassRate> ByClass);
