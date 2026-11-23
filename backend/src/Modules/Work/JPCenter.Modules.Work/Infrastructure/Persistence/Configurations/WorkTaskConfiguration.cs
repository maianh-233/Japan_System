using JPCenter.Modules.Work.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Work.Infrastructure.Persistence.Configurations;

/// <summary>
/// work.tasks + task_checklist_items + task_comments — database.md bảng 90, 91, 92.
/// Người làm / giao / theo dõi / viết bình luận (hr.employees) và đối tượng gắn (lead / lớp / HV) không FK (schema khác).
/// </summary>
internal sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> b)
    {
        b.ToTable("tasks");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.AssigneeId, x.Status });
        b.HasIndex(x => x.CreatorId);
        b.HasIndex(x => x.WatcherIds).HasMethod("gin");
        b.HasIndex(x => new { x.BranchId, x.Status, x.Due });
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Title).HasMaxLength(WorkTask.TitleMax);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.LinkLabel).HasMaxLength(300);
        b.Ignore(x => x.IsDone);

        b.OwnsMany(x => x.Checklist, c =>
        {
            c.ToTable("task_checklist_items");
            c.WithOwner().HasForeignKey("TaskId");
            c.Property<WorkTaskId>("TaskId");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedNever();
            c.HasIndex("TaskId");
            c.Property(x => x.Text).HasMaxLength(500);
        });
        b.Navigation(x => x.Checklist).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Comments, c =>
        {
            c.ToTable("task_comments");
            c.WithOwner().HasForeignKey("TaskId");
            c.Property<WorkTaskId>("TaskId");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedNever();
            c.HasIndex("TaskId");
            c.Property(x => x.Text).HasMaxLength(WorkTask.CommentMax);
            c.Property(x => x.ByName).HasMaxLength(200);
        });
        b.Navigation(x => x.Comments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
