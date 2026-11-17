using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JPCenter.Api.Common.Errors;

/// <summary>
/// Một chỗ bắt mọi exception (request-pipeline.md M3). Lỗi hệ thống → 500 chỉ kèm mã tra cứu, chi tiết nằm trong log.
/// Exception "biết trước" (JSON hỏng, sửa đè, client hủy) → mã HTTP phù hợp.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, code, title) = exception switch
        {
            BadHttpRequestException { InnerException: JsonException } or JsonException =>
                (400, "validation.malformed_json", "Dữ liệu gửi lên không đúng định dạng (sai kiểu hoặc có trường không được phép)"),
            BadHttpRequestException bad when bad.StatusCode == 413 =>
                (413, "validation.payload_too_large", "Dữ liệu gửi lên quá lớn"),
            BadHttpRequestException bad => (bad.StatusCode, "validation.bad_request", "Yêu cầu không hợp lệ"),
            DbUpdateConcurrencyException =>
                (409, "system.concurrency", "Dữ liệu vừa được người khác sửa. Vui lòng tải lại rồi thử lại."),
            OperationCanceledException when http.RequestAborted.IsCancellationRequested =>
                (499, "system.client_closed", "Yêu cầu đã bị hủy"),
            _ => (500, "system.unexpected", "Có lỗi hệ thống. Vui lòng thử lại hoặc báo quản trị kèm mã tra cứu."),
        };

        if (status >= 500) logger.LogError(exception, "Lỗi hệ thống khi xử lý {Method} {Path}", http.Request.Method, http.Request.Path);
        else logger.LogWarning("{Code} khi xử lý {Method} {Path}: {Message}", code, http.Request.Method, http.Request.Path, exception.Message);

        if (status == 499) return true;

        var problem = ProblemFactory.Create(status, code, title, http: http);
        if (env.IsDevelopment() && status >= 500) problem.Detail = exception.ToString();

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json", ct);
        return true;
    }
}
