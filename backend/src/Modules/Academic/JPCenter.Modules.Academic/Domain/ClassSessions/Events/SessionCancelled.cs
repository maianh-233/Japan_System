using JPCenter.SharedKernel.Domain;
using JPCenter.Modules.Academic.Domain.Classes;

namespace JPCenter.Modules.Academic.Domain.ClassSessions.Events;

/// <summary>Buổi bị hủy → Notifications báo HV / PH, gợi ý xếp học bù.</summary>
public sealed record SessionCancelled(ClassSessionId SessionId, ClassId ClassId, string Reason) : IDomainEvent;
