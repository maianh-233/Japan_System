namespace JPCenter.Modules.Students.Domain.Abroad;

/// <summary>Tình trạng 1 giấy tờ</summary>
public enum DocumentState
{
    /// <summary>Chưa có</summary>
    Missing,
    /// <summary>Đã nhận</summary>
    Received,
    /// <summary>Đã kiểm tra</summary>
    Verified,
}
