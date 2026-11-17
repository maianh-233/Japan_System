using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Branches;

/// <summary>Lỗi nhóm chi nhánh (user story 03) — mã trùng tài liệu.</summary>
public static class BranchErrors
{
    public static readonly Error CodeRequired = Error.Validation("system.branch.code_required", "Chưa nhập mã chi nhánh");
    public static readonly Error InvalidCode = Error.Validation("system.branch.invalid_code", "Mã chi nhánh gồm 2–4 chữ hoặc số");
    public static readonly Error NameRequired = Error.Validation("system.branch.name_required", "Chưa nhập tên chi nhánh");
    public static readonly Error InvalidInvoicePrefix = Error.Validation("system.branch.invalid_invoice_prefix", "Tiền tố hóa đơn gồm 2–4 chữ hoặc số");
    public static readonly Error InvalidStatus = Error.Conflict("system.branch.invalid_status", "Không chuyển được trạng thái chi nhánh");
    public static readonly Error BankIncomplete = Error.Validation("system.branch.bank_incomplete",
        "Nếu khai báo tài khoản nhận học phí, cần nhập đủ ngân hàng, số tài khoản và chủ tài khoản.");
    public static readonly Error NotFound = Error.NotFound("system.branch.not_found", "Không tìm thấy chi nhánh");
    /// <summary>CN-02.E3</summary>
    public static Error DuplicateCode(string code) => Error.Conflict("system.branch.duplicate_code", $"Mã {code} đã dùng cho chi nhánh khác");
    /// <summary>CN-02.E7 — 2 chi nhánh cùng tiền tố → số hóa đơn trùng</summary>
    public static Error DuplicateInvoicePrefix(string prefix) =>
        Error.Conflict("system.branch.duplicate_invoice_prefix", $"Tiền tố hóa đơn {prefix} đã dùng cho chi nhánh khác");
    /// <summary>CN-02.E6</summary>
    public static readonly Error ManagerInvalid = Error.Validation("system.branch.manager_invalid", "Quản lý chi nhánh phải là nhân viên đang làm việc");
    /// <summary>CN-06.E2</summary>
    public static Error HasStaff(int count) => Error.Conflict("system.branch.has_staff",
        $"Còn {count} nhân sự chưa chuyển chi nhánh hoặc chấm dứt công tác. Chuyển chi nhánh cho họ trước.");
    /// <summary>CN-06: gõ sai mã xác nhận</summary>
    public static readonly Error ConfirmMismatch = Error.Validation("system.branch.confirm_mismatch", "Mã xác nhận không đúng mã chi nhánh");
    public static readonly Error BankAccountInvalid = Error.Validation("system.branch.bank_account_invalid", "Số tài khoản gồm 6–20 chữ số");
}
