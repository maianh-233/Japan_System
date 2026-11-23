using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Tests.Domain;

public sealed class HandoverTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid From = Guid.NewGuid();
    private static readonly Guid To = Guid.NewGuid();
    private static Money M(decimal v) => Money.FromTrusted(v);

    private static Result<Handover> Create(decimal expected = 3_000_000, decimal counted = 3_000_000, string? note = null, Guid? to = null) =>
        Handover.Create(BranchId.New(), new DateOnly(2026, 6, 10), ShiftKey.Afternoon, From, to ?? To, M(expected), M(counted), note, " ",
            [(Guid.NewGuid(), " gọi lại 19h "), (Guid.Empty, "a"), (Guid.Empty, "b")], ["In biên lai", " "], Now);

    [Fact]
    public void BG01_kiem_tra_khi_tao()
    {
        Create(to: From).Error.Code.ShouldBe("work.handover.invalid_receiver");
        Create(counted: -1).Error.Code.ShouldBe("work.handover.invalid_cash");
        Create(counted: 2_900_000).Error.Code.ShouldBe("work.handover.note_required");
        var h = Create(counted: 2_900_000, note: "Trả lại tiền thừa").Value;
        (h.Status, h.Issues).ShouldBe((HandoverStatus.Pending, null));
        h.Leads.Count.ShouldBe(2);
        h.Leads[0].Note.ShouldBe("gọi lại 19h");
        h.PendingItems.Single().Text.ShouldBe("In biên lai");
    }

    [Fact]
    public void BG02_nhan_ca_khop_3_so_la_da_nhan()
    {
        var h = Create().Value;
        h.Receive(From, M(3_000_000), null, Now).Error.Code.ShouldBe("work.handover.not_receiver");
        h.TickItem(To, h.PendingItems[0].Id, true).Error.Code.ShouldBe("work.handover.not_received");
        h.Receive(To, M(3_000_000), " đủ ", Now).IsSuccess.ShouldBeTrue();
        (h.Status, h.ReceiverNote).ShouldBe((HandoverStatus.Received, "đủ"));
        h.Receive(To, M(3_000_000), null, Now).Error.Code.ShouldBe("work.handover.closed");
        h.TickItem(From, h.PendingItems[0].Id, true).Error.Code.ShouldBe("work.handover.not_receiver");
        h.TickItem(To, h.PendingItems[0].Id, true).IsSuccess.ShouldBeTrue();
        h.PendingItems[0].Done.ShouldBeTrue();
    }

    [Fact]
    public void BG02_lech_tien_khi_dem_lai_khac_hoac_nv_dem_lech_he_thong()
    {
        var a = Create().Value;
        a.Receive(To, M(2_950_000), null, Now);
        a.Status.ShouldBe(HandoverStatus.Disputed);

        var b = Create(counted: 2_900_000, note: "Thiếu").Value;
        b.Receive(To, M(2_900_000), null, Now);
        b.Status.ShouldBe(HandoverStatus.Disputed);
    }
}
