using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>
/// Dữ liệu để xét điều kiện cấp chứng nhận — Application gom từ Academic.Contracts (chuyên cần, bài thi cuối) và Finance.Contracts (còn nợ).
/// </summary>
/// <param name="AttendanceRate">Chuyên cần % (null = chưa có buổi nào)</param>
/// <param name="FinalExamTitle">Bài thi cuối cùng của lớp (null = không có bài thi bắt buộc)</param>
/// <param name="FinalExamPct">Điểm % bài thi cuối (null = chưa có điểm)</param>
/// <param name="FinalExamAbsent">Vắng bài thi cuối</param>
/// <param name="Debt">Học phí còn nợ của ghi danh</param>
public sealed record CertificateFacts(int? AttendanceRate, string? FinalExamTitle, decimal? FinalExamPct, bool FinalExamAbsent, Money Debt);
