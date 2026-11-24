using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Integrations.Common;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Integrations.TestIntegration;

/// <summary>
/// TH-04 — gọi thử dịch vụ. Thành công → cập nhật lần đồng bộ (hết lỗi). Lỗi xác thực → <c>error</c> + lỗi gần nhất; lỗi tạm chỉ ghi nhật ký.
/// Chế độ thử luôn thành công, ghi "(thử)". Chưa kết nối → <c>system.integration.not_connected</c>.
/// </summary>
public sealed record TestIntegrationCommand(string Key) : ICommand<TestIntegrationResult>;

/// <param name="Ok">Gọi thử thành công</param>
/// <param name="Message">Nội dung đã ghi nhật ký</param>
public sealed record TestIntegrationResult(bool Ok, string Message, IntegrationDto Integration);

public sealed class TestIntegrationHandler(IntegrationCalls calls, IntegrationReader reader) : ICommandHandler<TestIntegrationCommand, TestIntegrationResult>
{
    public async Task<Result<TestIntegrationResult>> Handle(TestIntegrationCommand cmd, CancellationToken ct)
    {
        var integration = await calls.FindAsync(cmd.Key, ct);
        if (integration is null) return IntegrationErrors.Unknown;
        if (integration.Status == IntegrationStatus.Disconnected) return IntegrationErrors.NotConnected;

        var result = await calls.Gateway.TestAsync(integration.Key, calls.ConfigOf(integration), ct);
        calls.Record(integration, result);
        await calls.SaveAsync(ct);
        return new TestIntegrationResult(result.Ok, IntegrationCalls.LogText(result), await reader.ToDtoAsync(integration, ct));
    }
}
