using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.Hr.Contracts.IntegrationEvents;

/// <summary>
/// Đơn đổi ca đã duyệt xong (DN-02) → Work đổi người của 2 ca cho nhau (XC-05). Hr đã kiểm 2 ca còn nguyên lúc duyệt (DN-02.E6);
/// Work vẫn kiểm lại, lệch thì bỏ qua + ghi log.
/// </summary>
/// <param name="RequesterShiftId">Work.ShiftAssignment — ca của người gửi đơn, sẽ giao cho đồng nghiệp</param>
/// <param name="ColleagueShiftId">Work.ShiftAssignment — ca của đồng nghiệp, sẽ giao cho người gửi</param>
public sealed record ShiftSwapApproved(Guid RequestId, string Code, Guid RequesterId, Guid RequesterShiftId, Guid ColleagueId, Guid ColleagueShiftId)
    : IIntegrationEvent;
