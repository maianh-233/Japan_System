using System.Globalization;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp học viên, ghi danh, chứng nhận mẫu từ <c>students.seed.json</c> (xuất từ mock frontend) — chạy khi bảng học viên còn trống, sau lớp học.
/// Đi qua method domain (bảo lưu, cho nghỉ, hoàn thành, ẩn, cấp chứng nhận) để dữ liệu mẫu đúng quy tắc.
/// Mock không có hạn bảo lưu → bảo lưu đến hôm nay + 1 tháng, lý do lấy từ ghi chú mock. Ngày mock là giờ UTC → đổi sang ngày giờ Việt Nam.
/// </summary>
internal sealed class StudentSeeder(StudentsDbContext db, IClock clock, ILogger<StudentSeeder> logger) : IDataSeeder
{
    public int Order => 40;

    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Students.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<StudentSeedFile>(typeof(StudentSeeder).Assembly, "students.seed.json");
        var today = clock.Today;

        var students = new Dictionary<string, Student>();
        foreach (var s in data.Students)
        {
            var created = Student.Create(s.Code, s.FullName, s.Gender is null ? null : Enum<Gender>(s.Gender), Date(s.Dob), s.Phone, s.Email,
                s.Address, s.Note, Branch(s.BranchId), Date(s.JoinedAt) ?? today, today);
            if (created.IsFailure)
            {
                logger.LogWarning("Bỏ qua học viên mẫu {Code}: {Error}", s.Code, created.Error.Message);
                continue;
            }
            db.AddWithId(created.Value, SeedData.IdOf(s.Id));
            students[s.Id] = created.Value;
        }

        foreach (var e in data.Enrollments)
        {
            if (!students.TryGetValue(e.StudentId, out var student)) continue;
            var enrolledAt = Date(e.EnrolledAt) ?? today;
            var x = Enrollment.Create(student.Id, SeedData.IdOf(e.ClassId), e.BranchId is null ? student.BranchId : Branch(e.BranchId), enrolledAt);
            var ended = Date(e.EndedAt) ?? today;
            var done = e.Status switch
            {
                "reserved" => x.Reserve(today.AddMonths(1), ReasonOf(e.Note) ?? "Bảo lưu", today),
                "dropped" => x.Drop(ended, e.Note ?? "Nghỉ học"),
                "completed" => x.Complete(ended),
                _ => Result.Success(),
            };
            if (done.IsFailure) logger.LogWarning("Ghi danh mẫu {Id}: {Error}", e.Id, done.Error.Message);
            if (e.Certificate is { } c)
            {
                x.IssueCertificate(c.Code, At(c.IssuedAt) ?? clock.UtcNow, c.SentTo, Person(c.SentById), c.Exception is null, c.Exception);
                for (var i = 1; i < c.SendCount; i++) x.ResendCertificate(c.SentTo, Person(c.SentById));
            }
            db.AddWithId(x, SeedData.IdOf(e.Id));
        }

        // Ẩn sau cùng: chỉ học viên không còn ghi danh hiệu lực
        foreach (var s in data.Students.Where(s => s.Hidden))
        {
            if (!students.TryGetValue(s.Id, out var student)) continue;
            student.Hide([], At(s.HiddenAt) ?? clock.UtcNow);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã nạp {Students} học viên, {Enrollments} ghi danh mẫu", students.Count, data.Enrollments.Count);
    }

    /// <summary>"Bảo lưu đến hết tháng sau — đi công tác" → "đi công tác"</summary>
    private static string? ReasonOf(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;
        var i = note.IndexOf(" — ", StringComparison.Ordinal);
        return i >= 0 && note.StartsWith("Bảo lưu", StringComparison.Ordinal) ? note[(i + 3)..] : note;
    }

    private static BranchId Branch(string mockId) => new(SeedData.IdOf(mockId));

    private static Guid Person(string? mockId) => mockId is null ? Guid.Empty : SeedData.IdOf(mockId);

    private static T Enum<T>(string value) where T : struct, global::System.Enum => SnakeCaseEnumConverter<T>.Parse(value);

    /// <summary>"2026-01-19" hoặc ISO UTC "2026-01-19T17:00:00Z" → ngày theo giờ Việt Nam</summary>
    private static DateOnly? Date(string? value)
    {
        if (value is null) return null;
        if (value.Length <= 10) return DateOnly.Parse(value, CultureInfo.InvariantCulture);
        return DateOnly.FromDateTime(DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).ToOffset(Vietnam).DateTime);
    }

    private static DateTimeOffset? At(string? iso) => iso is null ? null : DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture).ToUniversalTime();

    private sealed record StudentSeedFile(List<SeedStudent> Students, List<SeedEnrollment> Enrollments);

    private sealed record SeedStudent(string Id, string Code, string FullName, string? Gender, string? Dob, string? Phone, string? Email,
        string? Address, string? Note, string BranchId, string? JoinedAt, bool Hidden, string? HiddenAt);

    private sealed record SeedEnrollment(string Id, string StudentId, string ClassId, string? BranchId, string Status, string EnrolledAt,
        string? EndedAt, string? Note, SeedCertificate? Certificate);

    private sealed record SeedCertificate(string Code, string IssuedAt, string SentTo, string? SentById, int SendCount, string? Exception);
}
