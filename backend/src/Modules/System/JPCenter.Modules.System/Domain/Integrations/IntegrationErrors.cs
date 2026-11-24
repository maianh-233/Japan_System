using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Integrations;

public static class IntegrationErrors
{
    /// <summary>TH-02.E3</summary>
    public static readonly Error Unknown = Error.NotFound("system.integration.unknown", "Không có tích hợp này");
    /// <summary>TH-03</summary>
    public static readonly Error UnknownEvent = Error.Validation("system.integration.unknown_event", "Tích hợp không có việc này");
    /// <summary>TH-03, TH-07</summary>
    public static readonly Error NotConnected = Error.Conflict("system.integration.not_connected", "Tích hợp chưa kết nối");

    /// <summary>TH-02.E1 — thiếu trường bắt buộc (tên trường trong thông điệp)</summary>
    public static Error MissingField(IntegrationField field) =>
        Error.Validation("system.integration.missing_field", $"Nhập {field.Label}") with
        {
            FieldErrors = new Dictionary<string, string[]> { [field.Key] = [$"Nhập {field.Label}"] },
        };
}
