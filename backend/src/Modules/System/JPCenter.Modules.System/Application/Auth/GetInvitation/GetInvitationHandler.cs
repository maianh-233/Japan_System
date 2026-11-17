using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.GetInvitation;

public sealed class GetInvitationHandler(ISystemDbContext db, InvitationLookup invitations, IEmployeeDirectory employees)
    : IQueryHandler<GetInvitationQuery, InvitationDto>
{
    public async Task<Result<InvitationDto>> Handle(GetInvitationQuery query, CancellationToken ct)
    {
        if (await invitations.FindAsync(query.Token, ct) is not var (token, account)) return AuthErrors.InviteInvalid;

        var employee = await employees.FindAsync(account.EmployeeId, ct);
        var roleIds = account.Roles.Select(r => r.RoleId).ToList();
        var roles = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).ToListAsync(ct);
        return new InvitationDto(employee?.FullName ?? account.Email, account.Email, roles, token.ExpiresAt);
    }
}
