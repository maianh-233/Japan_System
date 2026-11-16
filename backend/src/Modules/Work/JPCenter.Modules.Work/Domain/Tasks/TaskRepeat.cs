namespace JPCenter.Modules.Work.Domain.Tasks;

/// <summary>Lặp lại</summary>
public enum TaskRepeat
{
    /// <summary>Không lặp</summary>
    None,
    /// <summary>Hằng ngày</summary>
    Daily,
    /// <summary>Hằng tuần</summary>
    Weekly,
    /// <summary>Hằng tháng</summary>
    Monthly,
}
