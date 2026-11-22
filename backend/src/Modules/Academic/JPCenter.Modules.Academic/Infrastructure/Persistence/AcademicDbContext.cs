using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.Modules.Academic.Domain.Flashcards;
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
/// lời mời dạy thay, lên lớp, nhận xét cuối khóa (user story 07), ngân hàng câu hỏi (11), bài tập, kỳ thi, kết quả thi, thông báo lớp,
/// đánh giá GV, flashcard (user story 12).
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
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<ClassExam> ClassExams => Set<ClassExam>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<ClassAnnouncement> ClassAnnouncements => Set<ClassAnnouncement>();
    public DbSet<TeacherReview> TeacherReviews => Set<TeacherReview>();
    public DbSet<FlashDeck> FlashDecks => Set<FlashDeck>();
    public DbSet<CardProgress> CardProgress => Set<CardProgress>();
}
