using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.Common;

/// <summary>Kỳ thi (đang theo dõi để sửa) + lớp, nếu người gọi được thao tác lớp (ngoài phạm vi nhân viên → 404, GV không dạy → 403)</summary>
public sealed class ExamFinder(IAcademicDbContext db, LearningAccess access)
{
    public async Task<Result<(ClassExam Exam, Class Class)>> FindAsync(Guid id, bool asTeacher, PermissionAction action, CancellationToken ct)
    {
        var exam = await db.ClassExams.FirstOrDefaultAsync(e => e.Id == new ClassExamId(id), ct);
        if (exam is null) return ExamErrors.NotFound;
        var cls = await access.ClassAsync(exam.ClassId.Value, asTeacher, action, ct);
        if (cls.IsFailure) return cls.Error.Type == ErrorType.NotFound ? ExamErrors.NotFound : cls.Error;
        return (exam, cls.Value);
    }
}
