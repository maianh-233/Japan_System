namespace JPCenter.Modules.Academic.Application.Courses.Common;

/// <summary>
/// Lớp đang dùng khóa / lộ trình — cho các quy tắc KH-04.E1/E2 (khóa mã, hình thức), KH-06.E2 (không gỡ lộ trình đang dạy),
/// LT-11 (xóa / lưu trữ), cột "lớp đang mở".
/// <para>⏳ Lớp học chưa lưu DB (giai đoạn 14 — user story 07): tạm trả rỗng; khi có bảng <c>classes</c> chỉ cần sửa class này.</para>
/// </summary>
public sealed class ClassUsage
{
    /// <summary>Mã lớp đang mở (chờ khai giảng / đang học / tạm dừng) theo khóa</summary>
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> OpenClassesByCourseAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(new Dictionary<Guid, IReadOnlyList<string>>());

    /// <summary>Khóa đã từng có lớp (kể cả đã kết thúc)</summary>
    public Task<bool> CourseHasClassesAsync(Guid courseId, CancellationToken ct) => Task.FromResult(false);

    /// <summary>Mã lớp đang mở học theo lộ trình (lọc theo khóa nếu có)</summary>
    public Task<IReadOnlyList<string>> OpenClassesUsingAsync(Guid curriculumId, Guid? courseId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    /// <summary>Số lớp (mọi trạng thái) học theo từng lộ trình</summary>
    public Task<IReadOnlyDictionary<Guid, int>> ClassCountByCurriculumAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
}
