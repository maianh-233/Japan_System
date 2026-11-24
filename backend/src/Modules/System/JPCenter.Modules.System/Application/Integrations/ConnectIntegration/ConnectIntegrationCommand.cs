using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Integrations.Common;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Integrations.ConnectIntegration;

/// <summary>
/// TH-02 — kết nối / kết nối lại: trường bí mật được mã hóa trước khi lưu (quy tắc vàng 1); để trống = giữ khóa cũ (E2); trường lạ bị bỏ (E4).
/// Nhật ký hệ thống <c>setting</c> ghi ở endpoint.
/// </summary>
/// <param name="Account">Tài khoản / trang hiển thị (tùy chọn)</param>
public sealed record ConnectIntegrationCommand(string Key, IReadOnlyDictionary<string, string?> Config, string? Account) : ICommand<IntegrationDto>;

public sealed class ConnectIntegrationValidator : AbstractValidator<ConnectIntegrationCommand>
{
    public ConnectIntegrationValidator()
    {
        RuleFor(x => x.Config).NotNull().Must(c => c is null || c.Count <= 20).WithMessage("Quá nhiều trường cấu hình");
        RuleForEach(x => x.Config).Must(kv => (kv.Value?.Length ?? 0) <= 2000).WithMessage("Giá trị quá dài");
        RuleFor(x => x.Account).MaximumLength(200);
    }
}

public sealed class ConnectIntegrationHandler(ISystemDbContext db, IIntegrationSecrets secrets, IntegrationReader reader, ICurrentUser user, IClock clock)
    : ICommandHandler<ConnectIntegrationCommand, IntegrationDto>
{
    public async Task<Result<IntegrationDto>> Handle(ConnectIntegrationCommand cmd, CancellationToken ct)
    {
        var integration = await db.Integrations.FirstOrDefaultAsync(i => i.Key == cmd.Key, ct);
        if (integration is null) return IntegrationErrors.Unknown;

        var input = cmd.Config.ToDictionary(kv => kv.Key, kv =>
        {
            var value = kv.Value?.Trim();
            return integration.Spec.Field(kv.Key)?.Secret == true && !string.IsNullOrEmpty(value) ? secrets.Protect(value) : value;
        });
        var connected = integration.Connect(input, cmd.Account, user.AccountId ?? Guid.Empty, clock.UtcNow);
        if (connected.IsFailure) return connected.Error;
        await db.SaveChangesAsync(ct);
        return await reader.ToDtoAsync(integration, ct);
    }
}
