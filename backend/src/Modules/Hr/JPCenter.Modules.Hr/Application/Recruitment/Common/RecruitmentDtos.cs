using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;

namespace JPCenter.Modules.Hr.Application.Recruitment.Common;

/// <summary>Tin tuyển — khớp <c>Job</c> frontend + số liệu danh sách: đã nhận / cần tuyển, đang phỏng vấn, chờ offer, tổng ứng viên.</summary>
public sealed record JobDto(Guid Id, string Title, Guid BranchId, Department Department, EmployeeKind Kind, int Quantity, string? SalaryRange,
    JobStatus Status, DateOnly PostedAt, DateOnly Deadline, IReadOnlyList<string> Channels, string? Description, int Hired, int Interviewing,
    int Offering, int Candidates);

public sealed record InterviewDto(DateTimeOffset At, string Interviewer, Guid InterviewerId, InterviewMode Mode, decimal? Score, string? Note);

public sealed record OfferDto(decimal Salary, DateOnly StartDate, DateTimeOffset SentAt, bool? Accepted);

public sealed record CandidateNoteDto(DateTimeOffset At, string By, string Text);

/// <summary>Ứng viên — khớp <c>Candidate</c> frontend; <c>hasCv</c> = có file CV (mở bằng link tạm).</summary>
public sealed record CandidateDto(Guid Id, Guid JobId, string FullName, string Phone, string Email, string Source, CandidateStage Stage,
    DateTimeOffset AppliedAt, string? CvName, bool HasCv, string? Japanese, decimal? ExpectedSalary, InterviewDto? Interview, OfferDto? Offer,
    string? RejectReason, IReadOnlyList<CandidateNoteDto> Notes);

/// <summary>Kết quả thao tác ứng viên: ứng viên + tin (số liệu / trạng thái có thể đổi) + phiếu onboarding vừa tạo (nhận việc)</summary>
public sealed record CandidateResult(CandidateDto Candidate, JobDto Job, Guid? OnboardingId, IReadOnlyList<string> Warnings);
