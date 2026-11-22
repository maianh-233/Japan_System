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

namespace JPCenter.Modules.Academic.Application.Abstractions;

/// <summary>DbContext module Academic cho tầng Application (Infrastructure hiện thực bằng AcademicDbContext).</summary>
public interface IAcademicDbContext
{
    DbSet<Course> Courses { get; }
    DbSet<Curriculum> Curricula { get; }
    DbSet<Room> Rooms { get; }
    DbSet<RoomIssue> RoomIssues { get; }
    DbSet<Class> Classes { get; }
    DbSet<ClassSession> ClassSessions { get; }
    DbSet<SessionCancelRequest> SessionCancelRequests { get; }
    DbSet<SubstituteInvite> SubstituteInvites { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<PromotionCandidate> PromotionCandidates { get; }
    DbSet<StudentEvaluation> StudentEvaluations { get; }
    DbSet<Question> Questions { get; }
    DbSet<Assignment> Assignments { get; }
    DbSet<Submission> Submissions { get; }
    DbSet<ClassExam> ClassExams { get; }
    DbSet<ExamAttempt> ExamAttempts { get; }
    DbSet<ExamResult> ExamResults { get; }
    DbSet<ClassAnnouncement> ClassAnnouncements { get; }
    DbSet<TeacherReview> TeacherReviews { get; }
    DbSet<FlashDeck> FlashDecks { get; }
    DbSet<CardProgress> CardProgress { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
