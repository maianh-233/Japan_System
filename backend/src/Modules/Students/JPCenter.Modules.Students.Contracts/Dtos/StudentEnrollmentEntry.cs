namespace JPCenter.Modules.Students.Contracts.Dtos;

/// <summary>1 ghi danh của học viên. <paramref name="Status"/>: studying · reserved · completed · dropped · transferred</summary>
public sealed record StudentEnrollmentEntry(Guid EnrollmentId, Guid ClassId, string Status);
