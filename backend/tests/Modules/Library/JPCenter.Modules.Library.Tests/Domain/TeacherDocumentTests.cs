using JPCenter.Modules.Library.Domain.TeacherDocuments;

namespace JPCenter.Modules.Library.Tests.Domain;

/// <summary>User story 12, ND-10 — tài liệu GV gửi lớp</summary>
public sealed class TeacherDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Teacher = Guid.NewGuid(), Student = Guid.NewGuid(), Class = Guid.NewGuid();

    [Fact]
    public void Tai_lieu_GV_chi_nguoi_soan_sua_doi_file_thi_xem_lai()
    {
        var file = new TeacherDocFile("bai5.pdf", TeacherDocKind.Pdf, "library/teacher-docs/bai5.pdf", 2048);
        TeacherDocument.Create(Teacher, "Bài 5", null, null, [Class], null).Error.ShouldBe(TeacherDocumentErrors.ContentRequired);
        TeacherDocument.Create(Teacher, "Bài 5", null, file, [], null).Error.ShouldBe(TeacherDocumentErrors.ClassRequired);
        TeacherDocument.Create(Teacher, " ", null, file, [Class], null).Error.ShouldBe(TeacherDocumentErrors.TitleRequired);
        var doc = TeacherDocument.Create(Teacher, " Bài 5 ", " ", file, [Class, Class], Guid.NewGuid()).Value;
        (doc.Title, doc.Description, doc.ClassIds.Count, doc.IsStoredFile).ShouldBe(("Bài 5", (string?)null, 1, true));

        doc.MarkViewed(Student, Now).ShouldBeTrue();
        doc.MarkViewed(Student, Now).ShouldBeFalse();
        doc.Update(Guid.NewGuid(), "x", null, null, [Class], null).Error.ShouldBe(TeacherDocumentErrors.NotOwner);
        doc.Update(Teacher, "Bài 5 (sửa)", null, null, [Class], null).IsSuccess.ShouldBeTrue();
        doc.Views.Count.ShouldBe(1);
        doc.Update(Teacher, "Bài 5", null, new TeacherDocFile("Video", TeacherDocKind.Link, "https://youtu.be/x", null), [Class], null)
            .IsSuccess.ShouldBeTrue();
        (doc.Views.Count, doc.IsStoredFile, doc.Url).ShouldBe((0, false, "https://youtu.be/x"));
    }
}
