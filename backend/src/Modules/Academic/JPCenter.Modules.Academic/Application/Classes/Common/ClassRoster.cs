using JPCenter.Modules.Students.Contracts;

namespace JPCenter.Modules.Academic.Application.Classes.Common;

/// <summary>Sĩ số lớp = ghi danh hiệu lực (đang học + bảo lưu) — đọc từ module Học viên qua Students.Contracts (quy tắc vàng 1, user story 07).</summary>
public sealed class ClassRoster(IEnrollmentLookup enrollments)
{
    /// <summary>Sĩ số theo lớp (lớp chưa có ai → 0)</summary>
    public async Task<IReadOnlyDictionary<Guid, int>> EnrolledAsync(IReadOnlyCollection<Guid> classIds, CancellationToken ct)
    {
        var counts = await enrollments.ActiveCountByClassAsync(classIds, ct);
        return classIds.Distinct().ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }

    public async Task<int?> EnrolledAsync(Guid classId, CancellationToken ct) => (await EnrolledAsync([classId], ct))[classId];
}
