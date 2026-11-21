using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Leads.ImportLeads;

/// <summary>1 dòng file Excel (frontend đã đọc + ghép cột)</summary>
/// <param name="Source">Không nhận ra → <c>event</c> (frontend gửi null)</param>
/// <param name="BranchId">Trống → chi nhánh đang làm việc của người nhập</param>
/// <param name="AssigneeId">Không khớp Sale → tự chia</param>
public sealed record ImportLeadRow(int Row, string? FullName, string? Phone, string? Email, LeadSource? Source, string? Interest, Guid? BranchId,
    Guid? AssigneeId, string? Note);

public sealed record ImportRowMessage(int Row, string Message);

public sealed record ImportLeadsResult(int Created, IReadOnlyList<LeadDto> Leads, IReadOnlyList<ImportRowMessage> Errors,
    IReadOnlyList<ImportRowMessage> Warnings);

/// <summary>
/// CRM-03 — nhập lead từ Excel: 1 request ≤ 1.000 dòng (E4), dòng lỗi báo theo dòng và bỏ qua (E1), trùng SĐT với lead có sẵn / trong file
/// chỉ cảnh báo (E3 — frontend đã cho chọn bỏ dòng). Lưu cả lô trong 1 giao dịch (E5). Dòng không có Sale chạy chia lead lần lượt.
/// </summary>
public sealed record ImportLeadsCommand(List<ImportLeadRow> Rows) : ICommand<ImportLeadsResult>;

public sealed class ImportLeadsHandler(ICrmDbContext db, CrmAccess access, ICurrentUser user, LeadChecks checks, LeadCodes codes,
    LeadReader reader, LeadRouting routing, SalesTeam team, IAuditTrail audit) : ICommandHandler<ImportLeadsCommand, ImportLeadsResult>
{
    public const int MaxRows = 1000;

    public async Task<Result<ImportLeadsResult>> Handle(ImportLeadsCommand cmd, CancellationToken ct)
    {
        if (cmd.Rows.Count == 0) return Error.Validation("crm.import.empty", "File không có dòng nào");
        if (cmd.Rows.Count > MaxRows) return LeadErrors.ImportTooManyRows;

        var defaultBranch = access.Branches(CrmAccess.Leads, PermissionAction.Create) is { Count: > 0 } allowed
            ? allowed.First()
            : user.Access?.BranchIds.FirstOrDefault();
        var own = access.OwnOnly(CrmAccess.Leads, PermissionAction.Create);
        var existingPhones = (await db.Leads.AsNoTracking().IgnoreQueryFilters().Select(l => new { l.Phone, l.Code }).ToListAsync(ct))
            .GroupBy(x => x.Phone).ToDictionary(g => g.Key, g => g.First().Code);
        var next = await codes.NextNumberAsync(ct);
        var errors = new List<ImportRowMessage>();
        var warnings = new List<ImportRowMessage>();
        var created = new List<Lead>();

        foreach (var row in cmd.Rows)
        {
            var branch = row.BranchId ?? defaultBranch;
            if (branch is not { } b || b == Guid.Empty || !access.InBranch(CrmAccess.Leads, PermissionAction.Create, b))
            {
                errors.Add(new(row.Row, CrmAccess.BranchNotAllowed.Message));
                continue;
            }
            var input = new LeadInput(row.FullName, row.Phone, row.Email, row.Source ?? LeadSource.Event, b, row.Interest, row.Note, null, null,
                null, null);
            if (await checks.CheckAsync(input, newBranch: true, null, ct) is { IsFailure: true } bad)
            {
                errors.Add(new(row.Row, bad.Error.Message));
                continue;
            }
            var made = Lead.Create(Lead.CodeFor(next), input.ToInfo());
            if (made.IsFailure)
            {
                errors.Add(new(row.Row, made.Error.Message));
                continue;
            }
            var lead = made.Value;
            if (existingPhones.TryGetValue(lead.Phone, out var dup)) warnings.Add(new(row.Row, $"Trùng SĐT với lead {dup}"));
            else existingPhones[lead.Phone] = lead.Code;

            db.Leads.Add(lead);
            // Sale trong file không hợp lệ (nghỉ việc / khác chi nhánh) → tự chia (E2)
            var chosen = own ?? (row.AssigneeId is { } s && await team.CanTakeAsync(s, b, ct) ? s : null);
            await routing.AssignNewAsync(lead, chosen, $"{access.ActorName} (nhập Excel)", ct);
            created.Add(lead);
            next++;
        }

        if (created.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            audit.Record(AuditCategory.Data, AuditSeverity.Info, "Nhập lead từ Excel", $"{created.Count} lead", $"{created[0].Code} → {created[^1].Code}");
        }
        return new ImportLeadsResult(created.Count, await reader.ToDtosAsync(created, ct), errors, warnings);
    }
}
