using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Tasks;

/// <summary>Lỗi công việc (user story 14, CV-xx) — mã trùng tài liệu.</summary>
public static class WorkTaskErrors
{
    public static readonly Error NotFound = Error.NotFound("work.task.not_found", "Không tìm thấy công việc");
    /// <summary>CV-01.E1</summary>
    public static readonly Error TitleRequired = Error.Validation("work.task.title_required", "Chưa nhập tiêu đề công việc");
    /// <summary>CV-01.E2</summary>
    public static readonly Error AssigneeInvalid = Error.Validation("work.task.assignee_invalid", "Người làm phải là nhân viên đang làm việc trong phạm vi của bạn");
    /// <summary>CV-01.E3</summary>
    public static readonly Error RepeatNeedsDue = Error.Validation("work.task.repeat_needs_due", "Task lặp lại cần có hạn");
    /// <summary>CV-01.E4</summary>
    public static readonly Error LinkNotFound = Error.Validation("work.task.link_not_found", "Không tìm thấy lead / lớp / học viên được gắn");
    /// <summary>CV-02.E1</summary>
    public static readonly Error Forbidden = Error.Forbidden("work.task.forbidden", "Bạn không liên quan tới công việc này");
    /// <summary>CV-04</summary>
    public static readonly Error NotYourReport = Error.Forbidden("work.task.not_your_report", "Chỉ giao việc cho nhân viên dưới quyền bạn");
    public static readonly Error ItemRequired = Error.Validation("work.task.item_required", "Nhập nội dung mục checklist");
    public static readonly Error ItemNotFound = Error.NotFound("work.task.item_not_found", "Không tìm thấy mục checklist");
    /// <summary>CV-05</summary>
    public static readonly Error CommentInvalid = Error.Validation("work.task.comment_invalid", "Bình luận từ 1 đến 2000 ký tự");
}
