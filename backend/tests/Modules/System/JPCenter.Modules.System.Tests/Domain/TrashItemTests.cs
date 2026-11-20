using JPCenter.Modules.System.Domain.Trash;

namespace JPCenter.Modules.System.Tests.Domain;

public sealed class TrashItemTests
{
    private static readonly DateTimeOffset DeletedAt = new(2026, 11, 1, 3, 0, 0, TimeSpan.Zero);

    private static TrashItem Item(int keepDays = 30) =>
        TrashItem.Create("system.role", Guid.NewGuid(), "Lễ tân", null, null, Guid.NewGuid(), DeletedAt, keepDays).Value;

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public void So_ngay_giu_ngoai_khoang_bi_chan(int days) =>
        TrashItem.Create("system.role", Guid.NewGuid(), "x", null, null, Guid.NewGuid(), DeletedAt, days).Error.ShouldBe(TrashErrors.KeepDaysInvalid);

    [Fact]
    public void Han_xoa_han_tinh_tu_luc_xoa() => Item(7).PurgeAfter.ShouldBe(DeletedAt.AddDays(7));

    [Fact]
    public void Con_han_thi_khoi_phuc_duoc_va_dem_ngay_lam_tron_len()
    {
        var item = Item(30);
        var now = DeletedAt.AddDays(10).AddHours(1);
        item.IsExpired(now).ShouldBeFalse();
        item.DaysLeft(now).ShouldBe(20);
        item.EnsureRestorable(now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void TR_02_E1_qua_han_khong_khoi_phuc_duoc()
    {
        var item = Item(30);
        var now = DeletedAt.AddDays(30);
        item.IsExpired(now).ShouldBeTrue();
        item.DaysLeft(now).ShouldBe(0);
        item.EnsureRestorable(now).Error.ShouldBe(TrashErrors.Expired);
    }
}
