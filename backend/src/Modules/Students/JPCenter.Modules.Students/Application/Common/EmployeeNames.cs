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
using JPCenter.Modules.Hr.Contracts;

namespace JPCenter.Modules.Students.Application.Common;

/// <summary>Tên nhân viên (người gửi chứng nhận…) qua Hr.Contracts — 1 lần cho cả danh sách</summary>
public sealed class EmployeeNames(IEmployeeDirectory employees)
{
    public async Task<IReadOnlyDictionary<Guid, string>> OfAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Where(i => i != Guid.Empty).Distinct().ToList();
        if (wanted.Count == 0) return new Dictionary<Guid, string>();
        return (await employees.FindManyAsync(wanted, ct)).ToDictionary(x => x.Key, x => x.Value.FullName);
    }
}
