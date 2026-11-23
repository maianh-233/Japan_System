using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Tests.Domain;

public sealed class WorkTaskTests
{
    private static readonly DateOnly Today = new(2026, 6, 10);
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid Assignee = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly Guid Watcher = Guid.NewGuid();

    private static Result<WorkTask> Create(string title = "Gọi lại lead", DateOnly? due = null, TaskRepeat repeat = TaskRepeat.None) =>
        WorkTask.Create("TK-1", title, null, BranchId.New(), Assignee, Creator, [Watcher, Assignee, Watcher], TaskPriority.High, due, repeat, ["Gọi", " ", "Ghi chú"]);

    [Fact]
    public void CV01_kiem_tra_khi_tao()
    {
        Create(" ").Error.Code.ShouldBe("work.task.title_required");
        Create(new string('x', 201)).Error.Code.ShouldBe("work.task.title_required");
        Create(repeat: TaskRepeat.Daily).Error.Code.ShouldBe("work.task.repeat_needs_due");
        var t = Create().Value;
        t.WatcherIds.ShouldBe([Watcher]); // bỏ trùng + bỏ người làm
        t.Checklist.Select(c => c.Text).ShouldBe(["Gọi", "Ghi chú"]);
        (t.Involves(Assignee), t.Involves(Creator), t.Involves(Watcher), t.Involves(Guid.NewGuid())).ShouldBe((true, true, true, false));
    }

    [Fact]
    public void CV01_doi_nguoi_lam_bo_khoi_nguoi_theo_doi()
    {
        var t = Create().Value;
        var branch = BranchId.New();
        t.Reassign(Watcher, branch);
        (t.AssigneeId, t.BranchId).ShouldBe((Watcher, branch));
        t.WatcherIds.ShouldBeEmpty();
    }

    [Fact]
    public void CV02_hoan_thanh_va_mo_lai()
    {
        var t = Create().Value;
        t.Move(WorkTaskStatus.Doing, Now, Today, () => "TK-2").ShouldBeNull();
        t.Move(WorkTaskStatus.Done, Now, Today, () => "TK-2").ShouldBeNull(); // không lặp
        t.CompletedAt.ShouldBe(Now);
        t.Move(WorkTaskStatus.Doing, Now, Today, () => "TK-2");
        t.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void CV02_task_lap_sinh_lan_ke_tiep_dung_1_lan()
    {
        var t = Create(due: new DateOnly(2026, 1, 31), repeat: TaskRepeat.Monthly).Value;
        t.LinkTo(TaskLinkType.Lead, Guid.NewGuid(), "Lead A");
        t.TickChecklist(t.Checklist[0].Id, true);
        var next = t.Move(WorkTaskStatus.Done, Now, Today, () => "TK-2")!;
        (next.Code, next.Due, next.Status, next.LinkLabel).ShouldBe(("TK-2", new DateOnly(2026, 2, 28), WorkTaskStatus.Todo, "Lead A"));
        next.Checklist.ShouldAllBe(c => !c.Done);
        next.Checklist.Count.ShouldBe(2);
        next.WatcherIds.ShouldBe([Watcher]);

        t.Move(WorkTaskStatus.Doing, Now, Today, () => "TK-3");
        t.Move(WorkTaskStatus.Done, Now, Today, () => "TK-3").ShouldBeNull();

        var daily = Create(due: Today, repeat: TaskRepeat.Daily).Value;
        daily.Move(WorkTaskStatus.Done, Now, Today, () => "TK-4")!.Due.ShouldBe(Today.AddDays(1));
    }

    [Fact]
    public void CV05_checklist_va_binh_luan()
    {
        var t = Create().Value;
        t.AddChecklistItem(" ").Error.Code.ShouldBe("work.task.item_required");
        t.AddChecklistItem("Gửi báo giá").IsSuccess.ShouldBeTrue();
        t.Checklist[^1].Order.ShouldBe(3);
        t.TickChecklist(Guid.NewGuid(), true).Error.Code.ShouldBe("work.task.item_not_found");
        t.RemoveChecklistItem(t.Checklist[0].Id).IsSuccess.ShouldBeTrue();
        t.Checklist.Count.ShouldBe(2);
        t.AddComment(new string('x', 2001), Creator, "A", Now).Error.Code.ShouldBe("work.task.comment_invalid");
        t.AddComment(" Xong chưa? ", Creator, "A", Now).IsSuccess.ShouldBeTrue();
        t.Comments.Single().Text.ShouldBe("Xong chưa?");
    }
}
