using JPCenter.Modules.Students.Domain.Abroad;

namespace JPCenter.Modules.Students.Application.Abroad.ChangeAbroadCase;

/// <summary>Ngày mốc hồ sơ (HV-17 bước 2) — gửi đủ cả bộ, trường trống = xóa</summary>
public sealed record AbroadMilestones(DateOnly? InterviewDate, InterviewResult? InterviewResult, DateOnly? CoeSubmittedAt, DateOnly? CoeGrantedAt,
    DateOnly? VisaAppliedAt, DateOnly? VisaIssuedAt, DateOnly? FlightDate);
