namespace JPCenter.Modules.Work.Domain.Tasks;

/// <summary>Cột Kanban</summary>
public enum WorkTaskStatus
{
    /// <summary>Cần làm</summary>
    Todo,
    /// <summary>Đang làm</summary>
    Doing,
    /// <summary>Chờ duyệt</summary>
    Review,
    /// <summary>Xong</summary>
    Done,
}
