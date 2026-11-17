using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Branches;

/// <summary>Chi nhánh trung tâm. Bảng: system.branches</summary>
public sealed class Branch : AggregateRoot<BranchId>, IAuditable, ISoftDeletable
{
    private Branch() { }

    /// <summary>Mã ngắn, vd CG, Q1 — duy nhất</summary>
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Address { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    /// <summary>Quản lý chi nhánh (Hr.Employee)</summary>
    public Guid? ManagerId { get; private set; }
    /// <summary>Giờ mở cửa dạng chữ, vd "T2–T7 08:00–21:30 · CN 08:00–17:00"</summary>
    public string OpenHours { get; private set; } = string.Empty;
    public BranchStatus Status { get; private set; }
    public DateOnly OpenedAt { get; private set; }
    /// <summary>Tài khoản nhận học phí (VietQR)</summary>
    public string? BankName { get; private set; }
    public string? BankAccount { get; private set; }
    public string? BankHolder { get; private set; }
    /// <summary>Tiền tố số hóa đơn, thay {CN} trong mẫu mã hóa đơn</summary>
    public string InvoicePrefix { get; private set; } = null!;
    public string? Note { get; private set; }

    public static Result<Branch> Create(string code, string name, string address, string phone, string email,
        string invoicePrefix, DateOnly openedAt)
    {
        if (string.IsNullOrWhiteSpace(code)) return BranchErrors.CodeRequired;
        if (string.IsNullOrWhiteSpace(name)) return BranchErrors.NameRequired;
        return new Branch
        {
            Id = BranchId.New(), Code = code.Trim().ToUpperInvariant(), Name = name.Trim(), Address = address,
            Phone = phone, Email = email, InvoicePrefix = invoicePrefix, OpenedAt = openedAt, Status = BranchStatus.Preparing,
        };
    }

    public void SetBankAccount(string bank, string account, string holder) => (BankName, BankAccount, BankHolder) = (bank, account, holder);

    public void AssignManager(Guid? employeeId) => ManagerId = employeeId;

    public void SetDetails(string openHours, string? note) => (OpenHours, Note) = (openHours, note);

    public Result Activate()
    {
        if (Status == BranchStatus.Active) return BranchErrors.InvalidStatus;
        Status = BranchStatus.Active;
        return Result.Success();
    }

    public Result Close()
    {
        if (Status == BranchStatus.Closed) return BranchErrors.InvalidStatus;
        Status = BranchStatus.Closed;
        return Result.Success();
    }
}
