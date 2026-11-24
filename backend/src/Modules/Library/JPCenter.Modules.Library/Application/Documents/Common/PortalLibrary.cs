using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Library.Application.TeacherDocuments.Common;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Application.Documents.Common;

/// <summary>
/// TV-07 / TV-08 — phạm vi thư viện ở cổng. HV / PH: lớp có ghi danh hiệu lực (đang học / bảo lưu — Students.Contracts) của HV (PH chọn con);
/// không còn lớp → chỉ tài liệu dành cho mọi chi nhánh (TV-07.E1). GV: lớp đang dạy chưa kết thúc / hủy (Academic.Contracts).
/// </summary>
public sealed class PortalLibrary(ICurrentUser user, TeacherDocs people, IEnrollmentLookup enrollments, IClassLookup classes)
{
    public async Task<Result<PortalLibraryScope>> ScopeAsync(Guid? studentId, CancellationToken ct)
    {
        if (user.Access is { Portal: Portal.Teacher, EmployeeId: { } teacher })
        {
            var taught = (await classes.TaughtByAsync(teacher, ct)).Where(c => !c.IsClosed).ToList();
            return Build(DocAudience.Teacher, taught.Select(c => (c.BranchId, c.Level)));
        }
        if (user.Access?.Portal is not (Portal.Student or Portal.Parent)) return LibraryDocumentErrors.NotFound;
        var student = await people.StudentAsync(studentId, ct);
        if (student.IsFailure) return LibraryDocumentErrors.NotFound;
        var active = (await enrollments.OfStudentAsync(student.Value, ct)).Where(e => e.Status is "studying" or "reserved")
            .Select(e => e.ClassId).Distinct().ToList();
        var cls = await classes.FindManyAsync(active, ct);
        return Build(DocAudience.Student, cls.Values.Select(c => (c.BranchId, c.Level)));
    }

    private static PortalLibraryScope Build(DocAudience audience, IEnumerable<(Guid Branch, string? Level)> classes)
    {
        var list = classes.ToList();
        var levels = list.Select(c => JlptLevelText.Parse(c.Level)).OfType<JlptLevel>().ToHashSet();
        return new PortalLibraryScope(audience, list.Select(c => new BranchId(c.Branch)).Distinct().ToList(), levels);
    }
}
