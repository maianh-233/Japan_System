using System.Diagnostics;
using JPCenter.Application.Common.Messaging;
using JPCenter.SharedKernel.Primitives;
using Microsoft.Extensions.Logging;

namespace JPCenter.Application.Common.Behaviors;

/// <summary>Log tên use case, thời gian chạy và mã lỗi (nếu thất bại). KHÔNG log nội dung request (có thể chứa mật khẩu).</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var started = Stopwatch.GetTimestamp();
        var response = await next();
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (response.IsSuccess)
            logger.LogInformation("{UseCase} xong trong {ElapsedMs:0} ms", name, ms);
        else
            logger.LogWarning("{UseCase} thất bại {ErrorCode} sau {ElapsedMs:0} ms", name, response.Error.Code, ms);
        return response;
    }
}
