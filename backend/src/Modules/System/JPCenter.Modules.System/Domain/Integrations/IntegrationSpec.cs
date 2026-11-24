namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>Mô tả cố định của 1 loại tích hợp: trường cấu hình + các việc (event) dùng kênh này (story 15)</summary>
public sealed record IntegrationSpec(string Key, IntegrationCategory Category, IReadOnlyList<IntegrationField> Fields, IReadOnlyList<string> Events)
{
    public IntegrationField? Field(string key) => Fields.FirstOrDefault(f => f.Key == key);
}
