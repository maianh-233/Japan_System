namespace JPCenter.SharedKernel.Primitives;

/// <summary>Kết quả có giá trị. Đọc Value khi thất bại → lỗi lập trình (exception).</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Result thất bại: {Error.Code}");

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
