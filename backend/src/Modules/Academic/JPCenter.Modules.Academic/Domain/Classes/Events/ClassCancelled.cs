using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Academic.Domain.Classes.Events;

/// <summary>Trung tâm hủy lớp → Students kết thúc ghi danh, Finance hủy phần còn lại / hoàn tiền.</summary>
public sealed record ClassCancelled(ClassId ClassId, string Reason) : IDomainEvent;
