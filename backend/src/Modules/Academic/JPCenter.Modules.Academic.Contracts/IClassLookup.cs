using JPCenter.Modules.Academic.Contracts.Dtos;

namespace JPCenter.Modules.Academic.Contracts;

/// <summary>Tra cứu lớp học — cách DUY NHẤT module khác đọc lớp (CLAUDE.md mục 3). Lớp đã xóa không trả.</summary>
public interface IClassLookup
{
    Task<ClassSummary?> FindAsync(Guid classId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, ClassSummary>> FindManyAsync(IReadOnlyCollection<Guid> classIds, CancellationToken ct);

    /// <summary>Lớp (chưa hủy) GV là GV chính hoặc trợ giảng — Library kiểm "chỉ gửi tài liệu cho lớp mình dạy" (ND-10.E1)</summary>
    Task<IReadOnlyList<ClassSummary>> TaughtByAsync(Guid teacherId, CancellationToken ct);
}
