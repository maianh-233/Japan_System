using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.DeleteAssignment;

/// <summary>ND-01.E7 — xóa bài tập: xóa mềm (Thùng rác), bài nộp giữ nguyên để khôi phục</summary>
public sealed record DeleteAssignmentCommand(Guid Id, bool AsTeacher) : ICommand;

public sealed class DeleteAssignmentHandler(IAcademicDbContext db, LearningAccess access) : ICommandHandler<DeleteAssignmentCommand>
{
    public async Task<Result> Handle(DeleteAssignmentCommand cmd, CancellationToken ct)
    {
        var a = await db.Assignments.FirstOrDefaultAsync(x => x.Id == new AssignmentId(cmd.Id), ct);
        if (a is null) return AssignmentErrors.NotFound;
        var cls = await access.ClassAsync(a.ClassId.Value, cmd.AsTeacher, PermissionAction.Update, ct);
        if (cls.IsFailure) return cls.Error.Type == ErrorType.NotFound ? AssignmentErrors.NotFound : cls.Error;
        db.Assignments.Remove(a);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
