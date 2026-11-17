using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

public sealed class RoomTests
{
    private static Result<Room> Create(string name = " P.301 ", int capacity = 20, decimal? area = 40) =>
        Room.Create(new BranchId(Guid.NewGuid()), name, " Tầng 3 ", RoomType.Classroom, capacity, area,
            [Equipment.Projector, Equipment.Projector, Equipment.Aircon], null);

    [Fact]
    public void Tao_phong_chuan_hoa()
    {
        var r = Create().Value;
        (r.Name, r.Floor, r.Status, r.IsUsable).ShouldBe(("P.301", "Tầng 3", RoomStatus.Active, true));
        r.Equipment.ShouldBe([Equipment.Projector, Equipment.Aircon]);
    }

    [Fact]
    public void Quy_tac_phong()
    {
        Create(name: "").Error.Code.ShouldBe("academic.room.name_required");
        Create(capacity: 0).Error.Code.ShouldBe("academic.room.invalid_capacity");
        Create(area: 0).Error.Code.ShouldBe("academic.room.invalid_area");
        var r = Create().Value;
        r.Update("P.301", null, RoomType.Lab, 10, null, [], null, largestClass: 18).Error.Message.ShouldContain("(18)");
    }

    [Fact]
    public void Bao_tri_mo_lai_ngung()
    {
        var r = Create().Value;
        r.StartMaintenance(" ", null).Error.Code.ShouldBe("academic.room.maintenance_reason");
        r.StartMaintenance("Sơn tường", new DateOnly(2026, 12, 20)).IsSuccess.ShouldBeTrue();
        r.IsUsable.ShouldBeFalse();
        r.Reopen();
        (r.Status, r.MaintenanceReason).ShouldBe((RoomStatus.Active, null));

        r.Deactivate(hasOpenClasses: true).Error.Code.ShouldBe("academic.room.in_use");
        r.Deactivate(false).IsSuccess.ShouldBeTrue();
        r.StartMaintenance("x", null).Error.Code.ShouldBe("academic.room.inactive");
    }
}
