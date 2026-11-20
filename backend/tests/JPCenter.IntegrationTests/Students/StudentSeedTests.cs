using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.Modules.Students.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Students;

/// <summary>Giai đoạn 14 phase 2 — dữ liệu mẫu học viên / ghi danh / chứng nhận nạp đúng từ mock frontend.</summary>
[Collection(SeededCollection.Name)]
public sealed class StudentSeedTests(SeededApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Du_lieu_mau_hoc_vien_va_ghi_danh()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();

        var students = await db.Students.AsNoTracking().ToListAsync(Ct);
        students.Count.ShouldBe(36);
        var an = students.Single(s => s.Id == new StudentId(SeedData.IdOf("ST-1")));
        (an.Code, an.FullName, an.Phone).ShouldBe(("HV1000", "Nguyễn Văn An", "0912000111"));
        students.ShouldContain(s => s.Hidden && s.HiddenAt != null);

        var enrollments = await db.Enrollments.AsNoTracking().ToListAsync(Ct);
        enrollments.Count.ShouldBe(42);
        enrollments.Count(e => e.Status == EnrollmentStatus.Studying).ShouldBe(22);
        enrollments.Count(e => e.Status == EnrollmentStatus.Completed).ShouldBe(11);
        enrollments.Count(e => e.Status == EnrollmentStatus.Dropped).ShouldBe(4);
        var reserved = enrollments.Where(e => e.Status == EnrollmentStatus.Reserved).ToList();
        reserved.Count.ShouldBe(5);
        reserved.ShouldAllBe(e => e.ReservedUntil != null && e.Note!.StartsWith("Bảo lưu đến "));

        // Ghi danh mang chi nhánh của LỚP, chứng nhận lưu bảng riêng
        var e1 = enrollments.Single(e => e.Id == new EnrollmentId(SeedData.IdOf("E-1")));
        (e1.ClassId, e1.BranchId.Value, e1.Certificate?.Code).ShouldBe((SeedData.IdOf("C-N4K11"), SeedData.IdOf("cg"), "CN-2026-N4K11-0001"));
        enrollments.Count(e => e.Certificate != null).ShouldBeGreaterThan(5);

        // HV đã ẩn không còn ghi danh hiệu lực
        var hidden = students.Where(s => s.Hidden).Select(s => s.Id).ToHashSet();
        enrollments.ShouldNotContain(e => hidden.Contains(e.StudentId) && (e.Status == EnrollmentStatus.Studying || e.Status == EnrollmentStatus.Reserved));
    }
}
