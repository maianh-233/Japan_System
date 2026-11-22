namespace JPCenter.Modules.Academic.Application.Learning.Common;

/// <summary>Ai đang mở tệp</summary>
public enum LearningViewer
{
    /// <summary>Nhân viên (phạm vi trang Lớp học)</summary>
    Staff,
    /// <summary>GV của lớp</summary>
    Teacher,
    /// <summary>HV / PH (lớp đã ghi danh; bài nộp: chỉ của mình / của con)</summary>
    Learner,
}
