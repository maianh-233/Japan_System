namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>1 trường cấu hình của tích hợp. <paramref name="Secret"/> = mã hóa khi lưu, chỉ trả 4 ký tự cuối.</summary>
public sealed record IntegrationField(string Key, string Label, bool Secret = false, bool Required = true);
