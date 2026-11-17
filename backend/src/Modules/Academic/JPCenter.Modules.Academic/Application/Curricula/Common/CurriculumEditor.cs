using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Files;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Academic.Application.Curricula.Common;

/// <summary>
/// Khung chung cho mọi thao tác soạn nội dung (LT-05 → LT-08): nạp lộ trình → áp thay đổi của domain → lưu → dọn file không còn ai dùng
/// → ghi nhật ký → trả lộ trình mới nhất (frontend thay nguyên object).
/// </summary>
public sealed class CurriculumEditor(IAcademicDbContext db, CurriculumReader reader, IFileStorage storage, IAuditTrail audit,
    ILogger<CurriculumEditor> logger)
{
    public Task<Result<CurriculumDto>> EditAsync(Guid id, Func<Curriculum, Result> change, string action, CancellationToken ct) =>
        EditAsync(id, c => change(c) is { IsFailure: true } r ? r.Error : Result.Success<IReadOnlyList<Material>>([]), action, ct);

    /// <param name="change">Trả về tài liệu bị xóa khỏi lộ trình (để dọn file trong kho)</param>
    public async Task<Result<CurriculumDto>> EditAsync(Guid id, Func<Curriculum, Result<IReadOnlyList<Material>>> change, string action,
        CancellationToken ct)
    {
        var curriculum = await LoadAsync(new CurriculumId(id), ct);
        if (curriculum is null) return CurriculumErrors.NotFound;

        var result = change(curriculum);
        if (result.IsFailure) return result.Error;

        await db.SaveChangesAsync(ct);
        await DeleteUnusedFilesAsync(result.Value.Where(m => m.IsFile).Select(m => m.FileKey!), ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, action, curriculum.Name, $"{curriculum.Code} {curriculum.Version}");
        return (await reader.FindAsync(curriculum.Id, ct))!;
    }

    /// <summary>Nạp lộ trình (có theo dõi thay đổi) kèm toàn bộ nội dung</summary>
    public Task<Curriculum?> LoadAsync(CurriculumId id, CancellationToken ct) =>
        db.Curricula.AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <summary>
    /// Xóa file trong kho khi không còn tài liệu nào (kể cả bản sao, phiên bản khác, lộ trình trong Thùng rác) trỏ tới — LT-06.E8.
    /// Lỗi kho chỉ ghi log: file mồ côi không làm hỏng thao tác của người dùng.
    /// </summary>
    public async Task DeleteUnusedFilesAsync(IEnumerable<string> keys, CancellationToken ct)
    {
        foreach (var key in keys.Distinct())
        {
            var used = await db.Curricula.IgnoreQueryFilters()
                .SelectMany(c => c.Lessons).SelectMany(l => l.Chapters).SelectMany(ch => ch.Materials)
                .AnyAsync(m => m.FileKey == key, ct);
            if (used) continue;
            try
            {
                await storage.DeleteAsync(key, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Không xóa được file {Key} khỏi kho — để lại file mồ côi", key);
            }
        }
    }
}
