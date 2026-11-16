namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Mức hoàn thành nội dung bài của buổi</summary>
public enum LessonCoverage
{
    /// <summary>Dạy xong bài</summary>
    Full,
    /// <summary>Một phần (dời buổi sau)</summary>
    Partial,
    /// <summary>Chưa dạy được</summary>
    None,
}
