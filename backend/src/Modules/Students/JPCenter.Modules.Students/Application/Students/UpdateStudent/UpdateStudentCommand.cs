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
namespace JPCenter.Modules.Students.Application.Students.UpdateStudent;

/// <summary>HV-03 — sửa hồ sơ; đổi chi nhánh phải trong phạm vi sửa. Sửa đè → 409 (E5).</summary>
public sealed record UpdateStudentCommand(Guid Id, StudentInput Input, uint RowVersion) : ICommand<StudentDto>;

public sealed class UpdateStudentValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentValidator() => RuleFor(x => x.Input).SetValidator(new StudentInputValidator());
}

public sealed class UpdateStudentHandler(IStudentsDbContext db, StudentReader reader, StudentAccess access, IClock clock, IAuditTrail audit)
    : ICommandHandler<UpdateStudentCommand, StudentDto>
{
    public async Task<Result<StudentDto>> Handle(UpdateStudentCommand cmd, CancellationToken ct)
    {
        var s = await db.Students.FirstOrDefaultAsync(x => x.Id == new StudentId(cmd.Id), ct);
        if (s is null || !access.InBranch(StudentAccess.Profiles, PermissionAction.Update, s.BranchId.Value)) return StudentErrors.NotFound;
        if (!access.InBranch(StudentAccess.Profiles, PermissionAction.Update, cmd.Input.BranchId)) return StudentAccess.BranchNotAllowed;
        ((DbContext)db).Entry(s).Property("xmin").OriginalValue = cmd.RowVersion;
        var i = cmd.Input;
        if (s.Update(i.FullName, i.Gender, i.Dob, i.Phone, i.Email, i.Address, i.Note, new BranchId(i.BranchId), clock.Today) is { IsFailure: true } r)
            return r.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Sửa hồ sơ học viên", s.FullName, s.Code);
        return await reader.ReloadAsync(s.Id, ct);
    }
}
