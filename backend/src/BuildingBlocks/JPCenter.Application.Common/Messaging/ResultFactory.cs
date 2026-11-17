using System.Collections.Concurrent;
using System.Reflection;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Messaging;

/// <summary>Tạo kết quả thất bại cho kiểu <c>Result</c> hoặc <c>Result&lt;T&gt;</c> khi chỉ biết kiểu lúc chạy (dùng trong behavior).</summary>
public static class ResultFactory
{
    private static readonly MethodInfo GenericFailure = typeof(Result).GetMethods()
        .Single(m => m is { Name: nameof(Result.Failure), IsGenericMethodDefinition: true });

    private static readonly ConcurrentDictionary<Type, Func<Error, Result>> Cache = new();

    public static TResponse Failure<TResponse>(Error error) where TResponse : Result =>
        (TResponse)Cache.GetOrAdd(typeof(TResponse), static t =>
        {
            if (t == typeof(Result)) return Result.Failure;
            var method = GenericFailure.MakeGenericMethod(t.GetGenericArguments()[0]);
            return e => (Result)method.Invoke(null, [e])!;
        })(error);
}
