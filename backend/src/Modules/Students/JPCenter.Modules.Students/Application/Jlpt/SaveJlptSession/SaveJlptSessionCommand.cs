using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Jlpt.SaveJlptSession;

/// <summary>
/// HV-16 bước 1 — thêm (<paramref name="Id"/> null) / sửa kỳ thi: hạn nộp &lt; ngày thi &lt; ngày có kết quả, đủ lệ phí N5 → N1, ≥ 1 địa điểm (E1).
/// <paramref name="Fees"/>: "N5" → số tiền VND. Sửa lệ phí không đổi lệ phí các lượt đã đăng ký.
/// </summary>
public sealed record SaveJlptSessionCommand(Guid? Id, string? Label, DateOnly ExamDate, DateOnly RegClose, DateOnly ResultDate,
    IReadOnlyDictionary<string, decimal>? Fees, IReadOnlyList<string>? Venues) : ICommand<JlptSessionDto>;

public sealed class SaveJlptSessionHandler(IStudentsDbContext db, JlptReader reader) : ICommandHandler<SaveJlptSessionCommand, JlptSessionDto>
{
    public async Task<Result<JlptSessionDto>> Handle(SaveJlptSessionCommand cmd, CancellationToken ct)
    {
        var fees = new Dictionary<JlptLevel, Money>();
        foreach (var (key, amount) in cmd.Fees ?? new Dictionary<string, decimal>())
        {
            if (!Enum.TryParse<JlptLevel>(key, ignoreCase: true, out var level) || Money.Create(amount) is not { IsSuccess: true } m)
                return JlptErrors.InvalidFees;
            fees[level] = m.Value;
        }
        var venues = cmd.Venues ?? [];
        JlptSession session;
        if (cmd.Id is { } id)
        {
            var found = await db.JlptSessions.FirstOrDefaultAsync(s => s.Id == new JlptSessionId(id), ct);
            if (found is null) return JlptErrors.SessionNotFound;
            var r = found.Update(cmd.Label, cmd.ExamDate, cmd.RegClose, cmd.ResultDate, fees, venues);
            if (r.IsFailure) return r.Error;
            session = found;
        }
        else
        {
            var created = JlptSession.Create(cmd.Label, cmd.ExamDate, cmd.RegClose, cmd.ResultDate, fees, venues);
            if (created.IsFailure) return created.Error;
            session = created.Value;
            db.JlptSessions.Add(session);
        }
        await db.SaveChangesAsync(ct);
        return reader.ToDto(session);
    }
}
