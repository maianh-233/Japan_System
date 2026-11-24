using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Abroad.Common;

/// <summary>
/// HV-17 dùng chung: phạm vi (chi nhánh của HV lúc tạo hồ sơ), mẫu giấy tờ theo chương trình (giống frontend <c>DOC_TEMPLATES</c>),
/// kiểm người phụ trách là nhân viên có thật, dựng DTO.
/// </summary>
public sealed class AbroadDesk(IStudentsDbContext db, StudentAccess access, EmployeeNames names)
{
    private static readonly string[] StudyDocs =
        ["Hộ chiếu", "Bằng tốt nghiệp + học bạ", "Chứng chỉ JLPT / NAT", "Giấy khai sinh", "Sổ tiết kiệm (chứng minh tài chính)",
         "Xác nhận thu nhập người bảo lãnh", "Ảnh 4×3"];
    private static readonly string[] WorkDocs =
        ["Hộ chiếu", "CCCD", "Sơ yếu lý lịch", "Giấy khám sức khỏe", "Bằng cấp", "Chứng chỉ JLPT", "Chứng chỉ kỹ năng ngành", "Ảnh 4×3"];

    public static IReadOnlyList<string> Template(AbroadProgram program) => program == AbroadProgram.Study ? StudyDocs : WorkDocs;

    public static AbroadCaseInfo ToInfo(AbroadCaseInput i) =>
        new(i.Program, i.Partner, i.Destination, i.Job, i.Intake, i.JlptRequired, Money.FromTrusted(Math.Max(0, decimal.Round(i.FeeTotal))), i.AssigneeId);

    /// <summary>Hồ sơ trong phạm vi quyền <paramref name="action"/></summary>
    public async Task<AbroadCase?> FindAsync(Guid id, PermissionAction action, CancellationToken ct)
    {
        var c = await db.AbroadCases.FirstOrDefaultAsync(x => x.Id == new AbroadCaseId(id), ct);
        return c is not null && await access.CoversAsync(StudentAccess.Abroad, action, c.StudentId.Value, c.BranchId.Value, ct) ? c : null;
    }

    public async Task<bool> IsEmployeeAsync(Guid id, CancellationToken ct) => (await names.OfAsync([id], ct)).ContainsKey(id);

    public async Task<IReadOnlyList<AbroadCaseDto>> ToDtosAsync(IQueryable<AbroadCase> query, CancellationToken ct)
    {
        var rows = await (from c in query.AsNoTracking()
                          join s in db.Students.AsNoTracking().IgnoreQueryFilters() on c.StudentId equals s.Id
                          select new
                          {
                              C = c, s.Code, s.FullName, Created = EF.Property<DateTimeOffset>(c, "CreatedAt"), V = EF.Property<uint>(c, "xmin"),
                          }).ToListAsync(ct);
        var who = await names.OfAsync(rows.Select(r => r.C.AssigneeId), ct);
        return rows.OrderByDescending(r => r.Created).Select(r =>
        {
            var c = r.C;
            return new AbroadCaseDto(c.Id.Value, c.StudentId.Value, r.Code, r.FullName, c.BranchId.Value, c.Program, c.Partner, c.Destination, c.Job,
                c.Intake, c.JlptRequired is { } lv ? JlptLevelText.Of(lv) : null, c.Stage, c.CancelledAt is { } at ? new AbroadCancelledDto(c.CancelReason ?? "", at) : null,
                c.Documents.Select(d => new AbroadDocDto(d.Id, d.Name, d.State)).ToList(),
                c.InterviewDate is { } iv ? new AbroadInterviewDto(iv, c.InterviewResult) : null,
                c.CoeSubmittedAt is null && c.CoeGrantedAt is null ? null : new AbroadCoeDto(c.CoeSubmittedAt, c.CoeGrantedAt),
                c.VisaAppliedAt is null && c.VisaIssuedAt is null ? null : new AbroadVisaDto(c.VisaAppliedAt, c.VisaIssuedAt),
                c.FlightDate, new AbroadFeeDto(c.FeeTotal.Amount, c.FeePaid.Amount), c.AssigneeId, who.GetValueOrDefault(c.AssigneeId) ?? "—",
                c.Notes.OrderBy(n => n.At).Select(n => new AbroadNoteDto(n.At, n.ByName, n.Text)).ToList(), r.Created, r.V);
        }).ToList();
    }

    public async Task<AbroadCaseDto> ReloadAsync(AbroadCaseId id, CancellationToken ct) =>
        (await ToDtosAsync(db.AbroadCases.Where(c => c.Id == id), ct))[0];
}
