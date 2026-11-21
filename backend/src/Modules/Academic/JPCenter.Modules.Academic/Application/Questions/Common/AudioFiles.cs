using JPCenter.Application.Common.Files;
using JPCenter.Modules.Academic.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Questions.Common;

/// <summary>File nghe của câu hỏi trong kho (ADR 0003). Câu nhân bản dùng chung khóa file → chỉ xóa khi không còn câu nào trỏ tới.</summary>
public static class AudioFiles
{
    public const string Folder = "academic/questions";
    public const string DemoFolder = "academic/questions/demo";

    public static async Task DeleteIfUnusedAsync(IAcademicDbContext db, IFileStorage storage, string? key, CancellationToken ct)
    {
        if (key is null || key.StartsWith(DemoFolder, StringComparison.Ordinal)) return; // file mẫu dùng chung — giữ
        if (await db.Questions.IgnoreQueryFilters().AnyAsync(q => q.AudioKey == key, ct)) return;
        await storage.DeleteAsync(key, ct);
    }
}
