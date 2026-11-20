using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Application.Students.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Students.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Students.Application.Students.UnhideStudent;

/// <summary>HV-05 — bỏ ẩn</summary>
public sealed record UnhideStudentCommand(Guid Id) : ICommand<StudentDto>;

public sealed class UnhideStudentHandler(IStudentsDbContext db, StudentReader reader, StudentAccess access, IAuditTrail audit)
    : ICommandHandler<UnhideStudentCommand, StudentDto>
{
    public async Task<Result<StudentDto>> Handle(UnhideStudentCommand cmd, CancellationToken ct)
    {
        var s = await db.Students.FirstOrDefaultAsync(x => x.Id == new StudentId(cmd.Id), ct);
        if (s is null || !access.InBranch(StudentAccess.Profiles, PermissionAction.Update, s.BranchId.Value)) return StudentErrors.NotFound;
        s.Unhide();
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Bỏ ẩn học viên", s.FullName, s.Code);
        return await reader.ReloadAsync(s.Id, ct);
    }
}
