namespace JPCenter.SharedKernel.Primitives;

/// <summary>Loại lỗi → API đổi sang HTTP status (400 / 404 / 409 / 403 / 500).</summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Failure,
}
