namespace JPCenter.Modules.Finance.Domain.Reminders;

/// <summary>Xếp hóa đơn còn nợ vào nhóm tuổi nợ (khớp <c>agingOf</c> frontend)</summary>
public static class DebtAgingRule
{
    public static DebtAging Of(DateOnly dueDate, DateOnly today)
    {
        var days = today.DayNumber - dueDate.DayNumber;
        return days <= 0 ? DebtAging.NotDue : days <= 30 ? DebtAging.Days1To30 : days <= 60 ? DebtAging.Days31To60 : DebtAging.Over60;
    }
}
