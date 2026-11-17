namespace JPCenter.Modules.Hr.Contracts.Dtos;

/// <summary>Số nhân sự CHƯA nghỉ việc có chi nhánh chính là chi nhánh đó.</summary>
/// <param name="Staff">Mọi loại: nhân viên + giáo viên + trợ giảng</param>
/// <param name="Teachers">Chỉ giáo viên</param>
public sealed record BranchHeadcount(int Staff, int Teachers);
