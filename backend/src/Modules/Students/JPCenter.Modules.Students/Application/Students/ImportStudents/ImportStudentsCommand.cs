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
namespace JPCenter.Modules.Students.Application.Students.ImportStudents;

/// <summary>Dòng lỗi (không nhập) — <c>Row</c> đánh số từ 1 theo thứ tự gửi lên</summary>
public sealed record ImportRowError(int Row, string Message);

/// <summary>Dòng trùng SĐT + họ tên với HV có sẵn (HV-04.E2) — chưa nhập khi <c>importDuplicates = false</c></summary>
public sealed record ImportDuplicate(int Row, string FullName, string ExistingCode);

public sealed record ImportStudentsResult(IReadOnlyList<StudentDto> Created, IReadOnlyList<ImportRowError> Errors, IReadOnlyList<ImportDuplicate> Duplicates);

/// <summary>
/// HV-04 — nhập học viên từ Excel (frontend đọc file, gửi 1 request ≤ 1000 dòng). Hồ sơ mới ở trạng thái "chưa xếp lớp".
/// Dòng sai → báo theo dòng, không nhập dòng đó (E1). Trùng SĐT + họ tên → trả danh sách để người dùng chọn, gửi lại với <paramref name="ImportDuplicates"/> (E2).
/// ⏳ Cột phụ huynh (E3) làm cùng Crm.
/// </summary>
public sealed record ImportStudentsCommand(List<StudentInput> Rows, bool ImportDuplicates) : ICommand<ImportStudentsResult>;

public sealed class ImportStudentsValidator : AbstractValidator<ImportStudentsCommand>
{
    public ImportStudentsValidator() =>
        RuleFor(x => x.Rows).NotEmpty().WithMessage("File không có dòng nào").Must(r => r.Count <= 1000).WithMessage("Tối đa 1000 dòng mỗi lần nhập");
}

public sealed class ImportStudentsHandler(IStudentsDbContext db, StudentCodes codes, StudentReader reader, StudentAccess access, IClock clock,
    IAuditTrail audit) : ICommandHandler<ImportStudentsCommand, ImportStudentsResult>
{
    public async Task<Result<ImportStudentsResult>> Handle(ImportStudentsCommand cmd, CancellationToken ct)
    {
        var today = clock.Today;
        var existing = (await db.Students.AsNoTracking().Where(s => s.Phone != null).Select(s => new { s.Phone, s.FullName, s.Code }).ToListAsync(ct))
            .GroupBy(s => Key(s.Phone, s.FullName)).ToDictionary(g => g.Key, g => g.First().Code);
        var next = await codes.NextNumberAsync(ct);
        var errors = new List<ImportRowError>();
        var duplicates = new List<ImportDuplicate>();
        var created = new List<Student>();

        for (var n = 0; n < cmd.Rows.Count; n++)
        {
            var row = cmd.Rows[n];
            if (!access.InBranch(StudentAccess.Profiles, PermissionAction.Create, row.BranchId))
            {
                errors.Add(new ImportRowError(n + 1, StudentAccess.BranchNotAllowed.Message));
                continue;
            }
            var s = Student.Create(Student.CodeFor(next), row.FullName ?? "", row.Gender, row.Dob, row.Phone, row.Email, row.Address, row.Note,
                new BranchId(row.BranchId), today, today);
            if (s.IsFailure)
            {
                errors.Add(new ImportRowError(n + 1, s.Error.Message));
                continue;
            }
            var key = Key(s.Value.Phone, s.Value.FullName);
            if (s.Value.Phone is not null && existing.TryGetValue(key, out var code) && !cmd.ImportDuplicates)
            {
                duplicates.Add(new ImportDuplicate(n + 1, s.Value.FullName, code));
                continue;
            }
            existing.TryAdd(key, s.Value.Code);
            db.Students.Add(s.Value);
            created.Add(s.Value);
            next++;
        }

        if (created.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            audit.Record(AuditCategory.Data, AuditSeverity.Info, "Nhập học viên từ Excel", $"{created.Count} học viên",
                $"{created[0].Code} → {created[^1].Code}");
        }
        var dtos = await reader.ReloadManyAsync(created.Select(c => c.Id).ToList(), ct);
        return new ImportStudentsResult(dtos, errors, duplicates);
    }

    private static string Key(string? phone, string name) => $"{phone}|{name.Trim().ToLowerInvariant()}";
}
