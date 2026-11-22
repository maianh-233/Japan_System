using JPCenter.Modules.Library.Domain.TeacherDocuments;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.Abstractions;

/// <summary>DbContext module Library cho tầng Application (Infrastructure hiện thực bằng LibraryDbContext).</summary>
public interface ILibraryDbContext
{
    DbSet<TeacherDocument> TeacherDocuments { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
