using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Infrastructure.Common.Persistence;

/// <summary>
/// Hỗ trợ nạp dữ liệu mẫu (xuất từ mock frontend bằng <c>npm run export-seed</c>).
/// Id mock dạng chuỗi ("e-huy", "cg", "R-SA") → Guid CỐ ĐỊNH, nên các module seed riêng mà vẫn trỏ đúng nhau
/// (tài khoản System trỏ nhân viên Hr, nhân viên Hr trỏ chi nhánh System...).
/// </summary>
public static class SeedData
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Guid cố định từ id mock — cùng id → cùng Guid ở mọi module, mọi lần chạy.</summary>
    public static Guid IdOf(string mockId)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("jpcenter-seed:" + mockId));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30); // đánh dấu version 3 (name-based)
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash, bigEndian: true);
    }

    /// <summary>Đọc file JSON nhúng trong assembly của module (EmbeddedResource *.seed.json).</summary>
    public static T Read<T>(Assembly assembly, string fileName)
    {
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<T>(stream, Json) ?? throw new InvalidOperationException($"Không đọc được {fileName}");
    }

    /// <summary>
    /// Gán Id cố định cho entity vừa tạo bằng factory (factory tự sinh Id ngẫu nhiên) rồi thêm vào context.
    /// Chỉ dùng khi nạp dữ liệu mẫu — code nghiệp vụ không bao giờ tự đặt Id.
    /// </summary>
    public static T AddWithId<T>(this DbContext db, T entity, Guid id) where T : class
    {
        var key = db.Model.FindEntityType(typeof(T))!.FindPrimaryKey()!.Properties.Single();
        var value = key.ClrType == typeof(Guid) ? (object)id : Activator.CreateInstance(key.ClrType, id)!;
        // Gán Id TRƯỚC rồi mới Add — Add duyệt cả đồ thị (entity con / owned: quyền của role, hồ sơ GV...)
        key.PropertyInfo!.SetValue(entity, value);
        db.Add(entity);
        return entity;
    }
}
