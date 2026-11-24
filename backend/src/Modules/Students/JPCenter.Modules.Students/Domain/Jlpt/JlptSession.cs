using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>Kỳ thi JLPT (tháng 7 & 12) + hạn nhận hồ sơ của trung tâm — HV-16 bước 1. Bảng: students.jlpt_sessions</summary>
public sealed class JlptSession : AggregateRoot<JlptSessionId>, IAuditable
{
    public const int MaxLabel = 50;

    private JlptSession() { }

    /// <summary>Nhãn, vd "Tháng 12/2026"</summary>
    public string Label { get; private set; } = null!;
    public DateOnly ExamDate { get; private set; }
    /// <summary>Hạn trung tâm nhận hồ sơ (trước hạn hội đồng vài ngày)</summary>
    public DateOnly RegistrationClose { get; private set; }
    public DateOnly ResultDate { get; private set; }
    /// <summary>Lệ phí theo cấp độ N5 → N1 (jsonb)</summary>
    public Dictionary<JlptLevel, Money> Fees { get; private set; } = [];
    /// <summary>Địa điểm thi được chọn</summary>
    public List<string> Venues { get; private set; } = [];

    public static Result<JlptSession> Create(string? label, DateOnly examDate, DateOnly registrationClose, DateOnly resultDate,
        IReadOnlyDictionary<JlptLevel, Money> fees, IEnumerable<string> venues)
    {
        var s = new JlptSession { Id = JlptSessionId.New() };
        var set = s.Update(label, examDate, registrationClose, resultDate, fees, venues);
        return set.IsFailure ? set.Error : s;
    }

    /// <summary>Sửa kỳ thi — E1: hạn nộp &lt; ngày thi &lt; ngày có kết quả; đủ lệ phí N5 → N1; ≥ 1 địa điểm</summary>
    public Result Update(string? label, DateOnly examDate, DateOnly registrationClose, DateOnly resultDate,
        IReadOnlyDictionary<JlptLevel, Money> fees, IEnumerable<string> venues)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > MaxLabel) return JlptErrors.LabelRequired;
        if (registrationClose >= examDate || resultDate <= examDate) return JlptErrors.InvalidDates;
        var levels = Enum.GetValues<JlptLevel>().Where(JlptScoring.IsExamLevel).ToList();
        if (levels.Any(l => !fees.TryGetValue(l, out var f) || f.Amount <= 0)) return JlptErrors.InvalidFees;
        var places = venues.Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToList();
        if (places.Count == 0) return JlptErrors.VenueRequired;
        (Label, ExamDate, RegistrationClose, ResultDate) = (label.Trim(), examDate, registrationClose, resultDate);
        Fees = levels.ToDictionary(l => l, l => fees[l]);
        Venues = places;
        return Result.Success();
    }

    /// <summary>HV-16.E6 — đã quá hạn nhận hồ sơ (cảnh báo, vẫn cho đăng ký)</summary>
    public bool IsClosed(DateOnly today) => today > RegistrationClose;

    public Money FeeFor(JlptLevel level) => Fees.GetValueOrDefault(level, Money.Zero);
}
