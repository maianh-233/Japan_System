using JPCenter.SharedKernel.Domain;
using JPCenter.Modules.Academic.Domain.Classes;

namespace JPCenter.Modules.Academic.Domain.ClassSessions.Events;

/// <summary>Buổi đã được xác nhận → Finance cộng vào lương GV (theo vai trò), trừ buổi trong gói 1-1.</summary>
public sealed record SessionConfirmed(ClassSessionId SessionId, ClassId ClassId, DateOnly Date, List<ConfirmedTeacher> Teachers) : IDomainEvent;
