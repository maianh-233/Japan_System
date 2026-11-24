namespace JPCenter.Modules.Students.Contracts.Dtos;

/// <summary>1 ghi danh cho báo cáo (BC-05, BC-06): sĩ số, HV đang học, ghi danh mới theo tháng.</summary>
/// <param name="ClassId">Academic.Class</param>
/// <param name="BranchId">Chi nhánh của lớp</param>
/// <param name="Status">studying · reserved · completed · transferred · dropped</param>
public sealed record EnrollmentFact(Guid StudentId, Guid ClassId, Guid BranchId, string Status, DateOnly EnrolledAt);
