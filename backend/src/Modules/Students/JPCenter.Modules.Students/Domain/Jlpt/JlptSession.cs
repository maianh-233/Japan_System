using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>Kỳ thi JLPT (tháng 7 & 12) + hạn nhận hồ sơ của trung tâm. Bảng: students.jlpt_sessions</summary>
public sealed class JlptSession : AggregateRoot<JlptSessionId>, IAuditable
{
    private JlptSession() { }

    /// <summary>Nhãn, vd "Tháng 12/2026"</summary>
    public string Label { get; private set; } = null!;
    public DateOnly ExamDate { get; private set; }
    /// <summary>Hạn trung tâm nhận hồ sơ (trước hạn hội đồng vài ngày)</summary>
    public DateOnly RegistrationClose { get; private set; }
    public DateOnly ResultDate { get; private set; }
    /// <summary>Lệ phí theo cấp độ (jsonb)</summary>
    public Dictionary<JlptLevel, Money> Fees { get; private set; } = [];
    /// <summary>Địa điểm thi được chọn</summary>
    public List<string> Venues { get; private set; } = [];

    public static Result<JlptSession> Create(string label, DateOnly examDate, DateOnly registrationClose, DateOnly resultDate,
        Dictionary<JlptLevel, Money> fees, List<string> venues)
    {
        if (registrationClose >= examDate) return Error.Validation("students.jlpt_session.invalid_dates", "Hạn nhận hồ sơ phải trước ngày thi");
        return new JlptSession
        {
            Id = JlptSessionId.New(), Label = label, ExamDate = examDate, RegistrationClose = registrationClose, ResultDate = resultDate,
            Fees = fees, Venues = venues,
        };
    }
}
