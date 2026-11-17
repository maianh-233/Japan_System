namespace JPCenter.SharedKernel.Primitives;

/// <summary>Lỗi nghiệp vụ: code dạng "crm.lead.duplicate_phone", thông điệp tiếng Việt.</summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Validation)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>Lỗi theo từng trường (khi validate): tên trường camelCase → các thông điệp.</summary>
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; init; }

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    /// <summary>Lỗi dữ liệu vào không hợp lệ, kèm lỗi từng trường.</summary>
    public static Error InvalidInput(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new("validation.failed", "Dữ liệu không hợp lệ", ErrorType.Validation) { FieldErrors = fieldErrors };
}
