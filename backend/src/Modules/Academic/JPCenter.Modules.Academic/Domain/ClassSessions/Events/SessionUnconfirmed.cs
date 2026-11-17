using JPCenter.SharedKernel.Domain;
using JPCenter.Modules.Academic.Domain.Classes;

namespace JPCenter.Modules.Academic.Domain.ClassSessions.Events;

/// <summary>Bỏ xác nhận buổi → Finance trừ khỏi lương GV tháng chưa chốt.</summary>
public sealed record SessionUnconfirmed(ClassSessionId SessionId, ClassId ClassId, DateOnly Date) : IDomainEvent;
