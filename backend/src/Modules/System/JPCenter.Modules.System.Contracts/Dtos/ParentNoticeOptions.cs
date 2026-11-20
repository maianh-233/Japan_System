namespace JPCenter.Modules.System.Contracts.Dtos;

/// <param name="WeeklyDay">0 = Chủ nhật … 6 = Thứ 7</param>
public sealed record ParentNoticeOptions(bool ArrivalNotice, int AbsentAlertMin, int UnderAge, bool WeeklyReport, int WeeklyDay, TimeOnly WeeklyTime);
