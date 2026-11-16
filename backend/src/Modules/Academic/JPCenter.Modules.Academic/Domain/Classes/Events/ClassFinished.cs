using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Academic.Domain.Classes.Events;

/// <summary>Lớp kết thúc → Students hoàn thành ghi danh, giao danh sách lên lớp cho Sale.</summary>
public sealed record ClassFinished(ClassId ClassId, DateOnly EndDate) : IDomainEvent;
