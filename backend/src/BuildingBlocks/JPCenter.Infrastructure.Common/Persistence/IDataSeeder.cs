namespace JPCenter.Infrastructure.Common.Persistence;

/// <summary>
/// Nạp dữ liệu mẫu cho 1 module (chỉ môi trường Development, khi <c>Seed:Enabled = true</c>).
/// Phải an toàn khi chạy nhiều lần: bảng đã có dữ liệu thì bỏ qua — KHÔNG xóa dữ liệu cũ.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Thứ tự chạy — module được tham chiếu (Hr: nhân viên) chạy trước module tham chiếu (System: tài khoản).</summary>
    int Order { get; }

    Task SeedAsync(CancellationToken ct);
}
