using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Tests.Domain;

public sealed class StudentTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);
    private static readonly BranchId Branch = new(Guid.NewGuid());

    private static Result<Student> Create(string name = "  Nguyễn   Văn  An ", DateOnly? dob = null, string? phone = "+84 912.345.678",
        string? email = " An@Mail.VN ") =>
        Student.Create(Student.CodeFor(1000), name, Gender.Male, dob ?? new DateOnly(2010, 5, 1), phone, email, " Hà Nội ", null, Branch, Today, Today);

    [Fact]
    public void Tao_hoc_vien_chuan_hoa_ten_sdt_email()
    {
        var s = Create().Value;
        (s.Code, s.FullName, s.Phone, s.Email, s.Address, s.Hidden).ShouldBe(("HV1000", "Nguyễn Văn An", "0912345678", "an@mail.vn", "Hà Nội", false));
    }

    [Theory]
    [InlineData(" ", "students.student.name_required")]
    public void HV03_E1_thieu_ho_ten(string name, string code) => Create(name).Error.Code.ShouldBe(code);

    [Fact]
    public void HV03_E2_sdt_email_sai()
    {
        Create(phone: "12345").Error.Code.ShouldBe("students.student.invalid_phone");
        Create(email: "khong-phai-email").Error.Code.ShouldBe("students.student.invalid_email");
        Create(phone: null, email: null).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void HV03_E3_ngay_sinh_tuong_lai() => Create(dob: Today.AddDays(1)).Error.Code.ShouldBe("students.student.invalid_dob");

    [Fact]
    public void Tuoi_tinh_theo_ngay_sinh()
    {
        var s = Create(dob: new DateOnly(2008, 11, 21)).Value;
        s.AgeAt(Today).ShouldBe(17);
        s.AgeAt(new DateOnly(2026, 11, 21)).ShouldBe(18);
    }

    [Fact]
    public void HV05_an_khi_khong_con_hoc_lop_nao()
    {
        var s = Create().Value;
        var blocked = s.Hide(["GVV_N3-K12"], DateTimeOffset.UtcNow);
        blocked.Error.Code.ShouldBe("students.student.still_studying");
        blocked.Error.Message.ShouldContain("GVV_N3-K12");
        s.Hidden.ShouldBeFalse();

        s.Hide([], DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        s.Hidden.ShouldBeTrue();
        s.Unhide();
        (s.Hidden, s.HiddenAt).ShouldBe((false, null));
    }
}
