namespace JPCenter.Modules.Students.Contracts.Dtos;

/// <summary>1 học viên trong danh sách lớp. <paramref name="Status"/>: studying · reserved</summary>
public sealed record RosterEntry(Guid StudentId, Guid EnrollmentId, string Code, string FullName, string Status, DateOnly? ReservedUntil);
