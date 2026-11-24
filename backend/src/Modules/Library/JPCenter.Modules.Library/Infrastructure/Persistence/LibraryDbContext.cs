using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.Modules.Library.Domain.TeacherDocuments;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Infrastructure.Persistence;

/// <summary>
/// DbContext module Library — schema <c>library</c>: tài liệu GV tự soạn gửi lớp (user story 12, giai đoạn 20).
/// Thư viện điện tử của trung tâm (documents, document_views — user story 16): giai đoạn 23.
/// </summary>
public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : ModuleDbContextBase(options), ILibraryDbContext
{
    public const string SchemaName = "library";

    protected override string Schema => SchemaName;

    public DbSet<TeacherDocument> TeacherDocuments => Set<TeacherDocument>();
    public DbSet<LibraryDocument> Documents => Set<LibraryDocument>();
    public DbSet<DocumentView> DocumentViews => Set<DocumentView>();
}
