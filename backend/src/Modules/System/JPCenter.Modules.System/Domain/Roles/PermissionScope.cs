namespace JPCenter.Modules.System.Domain.Roles;

/// <summary>Phạm vi của 1 quyền</summary>
public enum PermissionScope
{
    /// <summary>Chỉ dữ liệu của mình</summary>
    Own,
    /// <summary>Dữ liệu các chi nhánh được gán</summary>
    Branch,
    /// <summary>Toàn hệ thống</summary>
    All,
}
