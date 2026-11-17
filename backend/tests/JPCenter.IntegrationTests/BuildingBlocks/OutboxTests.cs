using System.Collections.Concurrent;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Infrastructure.Common.Outbox;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.BuildingBlocks;

/// <summary>Sự kiện chỉ dùng trong test Outbox. <paramref name="FailTimes"/>: số lần handler "dễ lỗi" ném lỗi trước khi thành công.</summary>
public sealed record OutboxPing(Guid Marker, int FailTimes) : IIntegrationEvent;

/// <summary>Đếm số lần mỗi handler nhận sự kiện theo Marker.</summary>
public static class OutboxPingLog
{
    public static readonly ConcurrentDictionary<(string Handler, Guid Marker), int> Calls = new();

    public static int Count(string handler, Guid marker) => Calls.GetValueOrDefault((handler, marker));

    public static int Hit(string handler, Guid marker) => Calls.AddOrUpdate((handler, marker), 1, (_, n) => n + 1);
}

public sealed class OutboxPingOkHandler : IIntegrationEventHandler<OutboxPing>
{
    public Task Handle(OutboxPing e, CancellationToken ct)
    {
        OutboxPingLog.Hit(nameof(OutboxPingOkHandler), e.Marker);
        return Task.CompletedTask;
    }
}

public sealed class OutboxPingFlakyHandler : IIntegrationEventHandler<OutboxPing>
{
    public Task Handle(OutboxPing e, CancellationToken ct)
    {
        var n = OutboxPingLog.Hit(nameof(OutboxPingFlakyHandler), e.Marker);
        if (n <= e.FailTimes) throw new InvalidOperationException($"Lỗi giả lần {n}");
        return Task.CompletedTask;
    }
}

/// <summary>Outbox (BuildingBlocks) trên PostgreSQL thật: ghi cùng giao dịch, gửi nền, thử lại, bỏ khi quá số lần.</summary>
[Collection(SeededCollection.Name)]
public sealed class OutboxTests(SeededApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task PublishAsync(OutboxPing ping, bool save = true)
    {
        await using var scope = api.Scope();
        scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().Publish(ping);
        if (save) await scope.ServiceProvider.GetRequiredService<SystemDbContext>().SaveChangesAsync(Ct);
    }

    private async Task<OutboxMessage?> MessageOf(Guid marker)
    {
        await using var scope = api.Scope();
        var all = await scope.ServiceProvider.GetRequiredService<SystemDbContext>().Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.Type == typeof(OutboxPing).FullName).ToListAsync(Ct);
        return all.FirstOrDefault(m => m.Payload.Contains(marker.ToString()));
    }

    private static async Task WaitUntil(Func<Task<bool>> condition, int seconds = 15)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            if (await condition()) return;
            await Task.Delay(100, Ct);
        }
        (await condition()).ShouldBeTrue("Hết thời gian chờ Outbox");
    }

    [Fact]
    public async Task Luu_xong_thi_su_kien_duoc_gui_toi_moi_handler()
    {
        var marker = Guid.NewGuid();
        await PublishAsync(new OutboxPing(marker, 0));

        await WaitUntil(async () => (await MessageOf(marker))?.ProcessedAt is not null);
        OutboxPingLog.Count(nameof(OutboxPingOkHandler), marker).ShouldBe(1);
        OutboxPingLog.Count(nameof(OutboxPingFlakyHandler), marker).ShouldBe(1);
        (await MessageOf(marker))!.HandledBy.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Khong_luu_thi_khong_gui()
    {
        var marker = Guid.NewGuid();
        await PublishAsync(new OutboxPing(marker, 0), save: false);

        await Task.Delay(1000, Ct);
        (await MessageOf(marker)).ShouldBeNull();
        OutboxPingLog.Count(nameof(OutboxPingOkHandler), marker).ShouldBe(0);
    }

    [Fact]
    public async Task Handler_loi_thi_thu_lai_va_handler_da_xong_khong_chay_lai()
    {
        var marker = Guid.NewGuid();
        await PublishAsync(new OutboxPing(marker, FailTimes: 2));

        await WaitUntil(async () => (await MessageOf(marker))?.ProcessedAt is not null);
        var m = (await MessageOf(marker))!;
        m.Attempts.ShouldBe(2);
        m.LastError.ShouldBeNull();
        OutboxPingLog.Count(nameof(OutboxPingFlakyHandler), marker).ShouldBe(3);
        OutboxPingLog.Count(nameof(OutboxPingOkHandler), marker).ShouldBe(1); // không bị gọi lại khi thử lại
    }

    [Fact]
    public async Task Qua_so_lan_thu_thi_danh_dau_that_bai_va_ngung_gui()
    {
        var marker = Guid.NewGuid();
        await PublishAsync(new OutboxPing(marker, FailTimes: 99));

        await WaitUntil(async () => (await MessageOf(marker))?.FailedAt is not null);
        var m = (await MessageOf(marker))!;
        m.Attempts.ShouldBe(SeededApiFactory.OutboxMaxAttempts);
        m.ProcessedAt.ShouldBeNull();
        m.LastError!.ShouldContain("Lỗi giả");

        var calls = OutboxPingLog.Count(nameof(OutboxPingFlakyHandler), marker);
        await Task.Delay(800, Ct);
        OutboxPingLog.Count(nameof(OutboxPingFlakyHandler), marker).ShouldBe(calls); // không thử thêm
    }
}
