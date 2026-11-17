using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

public sealed class CourseTests
{
    private static readonly BranchId Cg = new(Guid.NewGuid());

    private static Result<Course> Create(string code = "jlpt-n3", CourseFormat format = CourseFormat.Group, int? max = 15, decimal fee = 6_000_000,
        JlptLevel from = JlptLevel.N4, JlptLevel to = JlptLevel.N3, List<BranchId>? branches = null) =>
        Course.Create(code, " JLPT N3 nhóm ", CourseCategory.Jlpt, from, to, format, TeacherType.Vn, StudyMode.Offline, 48, 120, 2,
            Money.FromTrusted(fee), max, branches ?? [Cg], null);

    private static Result Update(Course c, string code = "JLPT-N3", CourseFormat format = CourseFormat.Group, TeacherType teacher = TeacherType.Vn,
        bool hasClasses = false, decimal fee = 6_500_000) =>
        c.Update(code, "JLPT N3", CourseCategory.Jlpt, JlptLevel.N4, JlptLevel.N3, format, teacher, StudyMode.Online, 50, 120, 2,
            Money.FromTrusted(fee), 15, [Cg], "mô tả", hasClasses);

    [Fact]
    public void Tao_khoa_chuan_hoa_ma_va_ten_trang_thai_nhap()
    {
        var c = Create().Value;
        c.Code.ShouldBe("JLPT-N3");
        c.Name.ShouldBe("JLPT N3 nhóm");
        c.Status.ShouldBe(CourseStatus.Draft);
    }

    [Theory]
    [InlineData("N", "academic.course.invalid_code")]
    [InlineData("JLPT N3", "academic.course.invalid_code")]
    public void Ma_sai_dinh_dang(string code, string error) => Create(code).Error.Code.ShouldBe(error);

    [Fact]
    public void Quy_tac_tao_khoa()
    {
        Create(to: JlptLevel.N5).Error.Code.ShouldBe("academic.course.invalid_levels");
        Create(max: null).Error.Code.ShouldBe("academic.course.max_students_required");
        Create(fee: 0).Error.Code.ShouldBe("academic.course.invalid_fee");
        Create(branches: []).Error.Code.ShouldBe("academic.course.branch_required");
    }

    [Fact]
    public void Khoa_1_kem_1_bo_si_so() => Create(format: CourseFormat.Private, max: 10).Value.MaxStudents.ShouldBeNull();

    [Fact]
    public void Co_lop_thi_khoa_ma_va_hinh_thuc()
    {
        var c = Create().Value;
        Update(c, code: "JLPT-N3-B", hasClasses: true).Error.Code.ShouldBe("academic.course.code_locked");
        Update(c, format: CourseFormat.Private, hasClasses: true).Error.Code.ShouldBe("academic.course.format_locked");
        Update(c, teacher: TeacherType.Jp, hasClasses: true).Error.Code.ShouldBe("academic.course.format_locked");
        Update(c, hasClasses: true).IsSuccess.ShouldBeTrue();
        c.Fee.Amount.ShouldBe(6_500_000);
        Update(c, code: "jlpt-n3-b").IsSuccess.ShouldBeTrue();
        c.Code.ShouldBe("JLPT-N3-B");
    }

    [Fact]
    public void Lo_trinh_dau_tien_tu_thanh_mac_dinh_doi_mac_dinh_va_go()
    {
        var c = Create().Value;
        var a = CurriculumId.New();
        var b = CurriculumId.New();
        c.LinkCurriculum(a, false, DateTimeOffset.UtcNow);
        c.DefaultCurriculumId.ShouldBe(a);

        c.LinkCurriculum(b, false, DateTimeOffset.UtcNow);
        c.DefaultCurriculumId.ShouldBe(a);
        c.LinkCurriculum(a, false, DateTimeOffset.UtcNow); // gắn lại không làm mất mặc định
        c.DefaultCurriculumId.ShouldBe(a);

        c.UnlinkCurriculum(a).Error.Code.ShouldBe("academic.course.default_curriculum");
        c.SetDefaultCurriculum(b).IsSuccess.ShouldBeTrue();
        c.Curricula.Count(x => x.IsDefault).ShouldBe(1);
        c.UnlinkCurriculum(a).IsSuccess.ShouldBeTrue();
        c.Curricula.Count.ShouldBe(1);
        c.SetDefaultCurriculum(a).Error.Code.ShouldBe("academic.course.curriculum_not_linked");
    }

    [Fact]
    public void Mo_tuyen_sinh_can_lo_trinh_mac_dinh_da_phat_hanh()
    {
        var c = Create().Value;
        c.Activate(true).Error.Code.ShouldBe("academic.course.no_default_curriculum");
        c.LinkCurriculum(CurriculumId.New(), true, DateTimeOffset.UtcNow);
        c.Activate(false).Error.Code.ShouldBe("academic.course.no_default_curriculum");
        c.Activate(true).IsSuccess.ShouldBeTrue();
        c.Status.ShouldBe(CourseStatus.Active);
        c.Pause().IsSuccess.ShouldBeTrue();
        c.Pause().Error.Code.ShouldBe("academic.course.not_active");
    }
}
