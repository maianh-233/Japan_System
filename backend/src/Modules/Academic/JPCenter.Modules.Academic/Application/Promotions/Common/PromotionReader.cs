using JPCenter.Application.Common.Time;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.Modules.Students.Contracts;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Promotions.Common;

public sealed record PromotionNoteDto(Guid Id, DateTimeOffset At, Guid ById, string By, string Text, string? Channel);

public sealed record InterestDto(DateTimeOffset At, InterestBy By);

/// <summary>Đề xuất lên lớp (nhân viên) — khớp <c>PromotionCandidate</c> ở <c>frontend/src/features/academic/promotion/types.ts</c></summary>
public sealed record PromotionDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid FromClassId,
    string FromClassCode,
    Guid? ToClassId,
    string? ToClassCode,
    bool SeatHeld,
    DateOnly? HoldUntil,
    PromotionStatus Status,
    PromotionSource Source,
    DateTimeOffset? HandedOverAt,
    Guid? SaleId,
    string? SaleName,
    InterestDto? Interest,
    string? DeclineReason,
    Guid? EnrollmentId,
    DateTimeOffset? ConfirmedAt,
    Guid? ConfirmedBy,
    IReadOnlyList<PromotionNoteDto> Notes,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy);

/// <summary>Lớp kỳ sau gợi ý cho HV trên cổng (DT-17 bước 3) — không có ghi chú tư vấn / Sale / học phí</summary>
public sealed record PromotionOfferDto(
    Guid Id,
    Guid StudentId,
    Guid FromClassId,
    string FromClassCode,
    Guid ToClassId,
    string ToClassCode,
    string ToClassName,
    DateOnly ToStartDate,
    IReadOnlyList<int> Weekdays,
    string StartTime,
    string EndTime,
    bool Online,
    bool SeatHeld,
    DateOnly? HoldUntil,
    InterestDto? Interest);

/// <summary>Đọc đề xuất lên lớp ra DTO (tên HV qua Students.Contracts, tên Sale / người ghi qua Hr.Contracts)</summary>
public sealed class PromotionReader(IAcademicDbContext db, PeopleNames people, IEnrollmentLookup enrollments)
{
    public async Task<IReadOnlyList<PromotionDto>> ToDtosAsync(IReadOnlyList<PromotionCandidate> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var ids = list.Select(p => p.Id).ToList();
        var created = await db.PromotionCandidates.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, At = EF.Property<DateTimeOffset>(p, "CreatedAt"), By = EF.Property<Guid?>(p, "CreatedBy") })
            .ToDictionaryAsync(x => x.Id, ct);
        var codes = await ClassCodesAsync(list.SelectMany(p => new[] { p.FromClassId, p.ToClassId ?? p.FromClassId }), ct);
        var students = await enrollments.NamesAsync(list.Select(p => p.StudentId).ToList(), ct);
        var sales = await people.OfAsync(list.Select(p => p.SaleId), ct);

        return list.Select(p => new PromotionDto(
            p.Id.Value, p.StudentId, students.GetValueOrDefault(p.StudentId, "—"), p.FromClassId.Value, codes.GetValueOrDefault(p.FromClassId, "—"),
            p.ToClassId?.Value, p.ToClassId is { } to ? codes.GetValueOrDefault(to) : null, p.SeatHeld, p.HoldUntil, p.Status, p.Source, p.HandedOverAt,
            p.SaleId, p.SaleId is { } s ? sales.GetValueOrDefault(s) : null,
            p.InterestAt is { } at ? new InterestDto(at, p.InterestBy!.Value) : null, p.DeclineReason, p.EnrollmentId, p.ConfirmedAt, p.ConfirmedBy,
            p.Notes.OrderBy(n => n.At).Select(n => new PromotionNoteDto(n.Id, n.At, n.By, n.ByName, n.Text, n.Channel)).ToList(),
            created.TryGetValue(p.Id, out var c) ? c.At : DateTimeOffset.MinValue, created.TryGetValue(p.Id, out var cb) ? cb.By : null))
            .OrderBy(d => d.StudentName, StringComparer.Create(new("vi-VN"), true))
            .ToList();
    }

    public async Task<IReadOnlyList<PromotionOfferDto>> OffersAsync(IReadOnlyList<PromotionCandidate> list, CancellationToken ct)
    {
        var targets = list.Where(p => p.ToClassId is not null).ToList();
        if (targets.Count == 0) return [];
        var classIds = targets.SelectMany(p => new[] { p.FromClassId, p.ToClassId!.Value }).Distinct().ToList();
        var classes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        return targets.Where(p => classes.ContainsKey(p.ToClassId!.Value)).Select(p =>
        {
            var to = classes[p.ToClassId!.Value];
            return new PromotionOfferDto(p.Id.Value, p.StudentId, p.FromClassId.Value, classes.GetValueOrDefault(p.FromClassId)?.Code ?? "—", to.Id.Value,
                to.Code, to.Name, to.ActualStartDate ?? to.PlannedStartDate, to.Weekdays, TimeText.Of(to.StartTime), TimeText.Of(to.EndTime), to.Online,
                p.SeatHeld, p.HoldUntil, p.InterestAt is { } at ? new InterestDto(at, p.InterestBy!.Value) : null);
        }).ToList();
    }

    private async Task<IReadOnlyDictionary<ClassId, string>> ClassCodesAsync(IEnumerable<ClassId> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => list.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
    }
}
