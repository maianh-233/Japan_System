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
using System.Globalization;

namespace JPCenter.Modules.Students.Application.Students.Common;

/// <summary>
/// Mã HV tiếp theo (HV1000, HV1001…) = số lớn nhất đã dùng + 1 (kể cả hồ sơ đã xóa mềm). 1 instance nên không cần sequence;
/// 2 người nhập cùng lúc trùng mã → UQ chặn, người sau thử lại.
/// </summary>
public sealed class StudentCodes(IStudentsDbContext db)
{
    public const int First = 1000;

    public async Task<int> NextNumberAsync(CancellationToken ct)
    {
        var codes = await db.Students.IgnoreQueryFilters().Select(s => s.Code).ToListAsync(ct);
        var max = codes.Select(c => c.StartsWith("HV", StringComparison.Ordinal) && int.TryParse(c[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(First - 1).Max();
        return Math.Max(max, First - 1) + 1;
    }
}
