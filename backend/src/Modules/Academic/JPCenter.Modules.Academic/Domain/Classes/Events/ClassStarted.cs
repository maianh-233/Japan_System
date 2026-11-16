using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Academic.Domain.Classes.Events;

/// <summary>Lớp khai giảng → Finance tính hạn các đợt học phí theo ngày khai giảng.</summary>
public sealed record ClassStarted(ClassId ClassId, DateOnly StartDate) : IDomainEvent;
