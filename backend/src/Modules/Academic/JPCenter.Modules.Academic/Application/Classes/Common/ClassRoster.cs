namespace JPCenter.Modules.Academic.Application.Classes.Common;

/// <summary>
/// Sĩ số lớp = ghi danh hiệu lực (studying + reserved) — đọc từ module Học viên (quy tắc vàng 1, user story 07).
/// <para>⏳ Students chưa có (giai đoạn 14): tạm trả "chưa biết" (null) — khai giảng không chặn theo sĩ số, sửa sĩ số max không kiểm.
/// Khi có <c>IEnrollmentLookup</c> trong Students.Contracts chỉ cần sửa class này.</para>
/// </summary>
public sealed class ClassRoster
{
    /// <summary>Sĩ số theo lớp; lớp không có trong kết quả = chưa biết</summary>
    public Task<IReadOnlyDictionary<Guid, int>> EnrolledAsync(IReadOnlyCollection<Guid> classIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

    public async Task<int?> EnrolledAsync(Guid classId, CancellationToken ct) =>
        (await EnrolledAsync([classId], ct)).TryGetValue(classId, out var n) ? n : null;
}
