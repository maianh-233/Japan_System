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

    /// <summary>Mã chi nhánh / tiền tố hóa đơn: 2–4 chữ hoặc số, viết hoa (CG, Q1, HCM)</summary>
    public static bool IsShortCode(string? value) =>
        value is { Length: >= 2 and <= 4 } && value.All(char.IsAsciiLetterOrDigit);

    /// <summary>Thêm chi nhánh (CN-02) — trạng thái "đang chuẩn bị". Tiền tố hóa đơn bỏ trống = theo mã.</summary>
    public static Result<Branch> Create(string code, string name, string address, string phone, string email,
        string? invoicePrefix, DateOnly openedAt)
    {
        if (string.IsNullOrWhiteSpace(code)) return BranchErrors.CodeRequired;
        var c = code.Trim().ToUpperInvariant();
        if (!IsShortCode(c)) return BranchErrors.InvalidCode;
        if (string.IsNullOrWhiteSpace(name)) return BranchErrors.NameRequired;
        var prefix = string.IsNullOrWhiteSpace(invoicePrefix) ? c : invoicePrefix.Trim().ToUpperInvariant();
        if (!IsShortCode(prefix)) return BranchErrors.InvalidInvoicePrefix;
        return new Branch
        {
            Id = BranchId.New(), Code = c, Name = name.Trim(), Address = address.Trim(), Phone = phone.Trim(), Email = email.Trim(),
            InvoicePrefix = prefix, OpenedAt = openedAt, Status = BranchStatus.Preparing,
        };
    }

    /// <summary>Sửa thông tin (CN-03). Mã không đổi được; trạng thái đổi qua <see cref="Open"/> / <see cref="Close"/> / <see cref="Reopen"/>.</summary>
    public Result Update(string name, string address, string phone, string email, string? invoicePrefix, DateOnly openedAt)
    {
        if (string.IsNullOrWhiteSpace(name)) return BranchErrors.NameRequired;
        var prefix = string.IsNullOrWhiteSpace(invoicePrefix) ? Code : invoicePrefix.Trim().ToUpperInvariant();
        if (!IsShortCode(prefix)) return BranchErrors.InvalidInvoicePrefix;
        (Name, Address, Phone, Email, InvoicePrefix, OpenedAt) = (name.Trim(), address.Trim(), phone.Trim(), email.Trim(), prefix, openedAt);
        return Result.Success();
    }

    /// <summary>Tài khoản nhận học phí (CN-04): nhập đủ cả 3 hoặc bỏ trống cả 3. Số tài khoản 6–20 chữ số, chủ tài khoản viết hoa.</summary>
    public Result SetBankAccount(string? bank, string? account, string? holder)
    {
        bank = bank?.Trim();
        account = account?.Replace(" ", "").Trim();
        holder = holder?.Trim();
        var filled = new[] { bank, account, holder }.Count(x => !string.IsNullOrEmpty(x));
        if (filled == 0)
        {
            (BankName, BankAccount, BankHolder) = (null, null, null);
            return Result.Success();
        }
        if (filled < 3) return BranchErrors.BankIncomplete;
        if (account!.Length is < 6 or > 20 || !account.All(char.IsAsciiDigit)) return BranchErrors.BankAccountInvalid;
        (BankName, BankAccount, BankHolder) = (bank, account, holder!.ToUpperInvariant());
        return Result.Success();
    }

    public bool HasBankAccount => BankAccount is not null;

    public void AssignManager(Guid? employeeId) => ManagerId = employeeId;

    public void SetDetails(string openHours, string? note) => (OpenHours, Note) = (openHours.Trim(), string.IsNullOrWhiteSpace(note) ? null : note.Trim());

    /// <summary>Khai trương (CN-05): đang chuẩn bị → hoạt động. Khai trương sớm hơn dự kiến → ngày khai trương = hôm nay.</summary>
    public Result Open(DateOnly today)
    {
        if (Status != BranchStatus.Preparing) return BranchErrors.InvalidStatus;
        Status = BranchStatus.Active;
        if (OpenedAt > today) OpenedAt = today;
        return Result.Success();
    }

    /// <summary>Đóng (CN-06): từ hoạt động hoặc đang chuẩn bị (bỏ dự án mở). Điều kiện lớp / nhân sự kiểm ở Application.</summary>
    public Result Close()
    {
        if (Status == BranchStatus.Closed) return BranchErrors.InvalidStatus;
        Status = BranchStatus.Closed;
        return Result.Success();
    }

    /// <summary>Mở lại chi nhánh đã đóng (CN-07)</summary>
    public Result Reopen()
    {
        if (Status != BranchStatus.Closed) return BranchErrors.InvalidStatus;
        Status = BranchStatus.Active;
        return Result.Success();
    }
}
