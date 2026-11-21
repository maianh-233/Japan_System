using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.Modules.Academic.Domain.Reviews;
using JPCenter.Modules.Academic.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence;

/// <summary>
/// DbContext module Academic — schema <c>academic</c>: khóa học + lộ trình (user story 09), phòng, lớp, buổi học, đơn hủy buổi / xin nghỉ,
/// lời mời dạy thay, lên lớp, nhận xét cuối khóa (user story 07). Ngân hàng câu hỏi, bài tập... thêm khi làm các story đó.
/// </summary>
public sealed class AcademicDbContext(DbContextOptions<AcademicDbContext> options) : ModuleDbContextBase(options), IAcademicDbContext
{
    public const string SchemaName = "academic";

    protected override string Schema => SchemaName;

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Curriculum> Curricula => Set<Curriculum>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomIssue> RoomIssues => Set<RoomIssue>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<SessionCancelRequest> SessionCancelRequests => Set<SessionCancelRequest>();
    public DbSet<SubstituteInvite> SubstituteInvites => Set<SubstituteInvite>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<PromotionCandidate> PromotionCandidates => Set<PromotionCandidate>();
    public DbSet<StudentEvaluation> StudentEvaluations => Set<StudentEvaluation>();
    public DbSet<Question> Questions => Set<Question>();
}
