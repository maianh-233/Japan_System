namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>Phiếu duyệt vừa mở: Id (System.ApprovalRequest) + mã hiển thị DY-yyMM-0001.</summary>
public sealed record ApprovalTicket(Guid Id, string Code);
