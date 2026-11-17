using JPCenter.Api.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace JPCenter.Api.Common.Validation;

/// <summary>
/// Chặn sớm body quá lớn theo <c>Content-Length</c> (mặc định 1 MB, endpoint nới bằng <c>.WithBodyLimit()</c>) → 413 có mã lỗi.
/// Kestrel vẫn giới hạn thêm khi body gửi kiểu chunked (không có Content-Length).
/// </summary>
public sealed class BodySizeMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext http)
    {
        var limit = http.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize
            ?? RequestPipelineExtensions.DefaultBodyLimit;
        if (http.Request.ContentLength is { } length && length > limit)
            return ProblemFactory.WriteAsync(http, StatusCodes.Status413PayloadTooLarge, "validation.payload_too_large",
                $"Dữ liệu gửi lên quá lớn (tối đa {limit / 1024 / 1024.0:0.#} MB).");
        return next(http);
    }
}
