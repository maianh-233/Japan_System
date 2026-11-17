using System.Text.Json;
using System.Text.Json.Serialization;
using JPCenter.Api.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Api.Common;

public static class ApiCommonServiceCollectionExtensions
{
    /// <summary>JSON chuẩn của API + ProblemDetails + bắt lỗi toàn cục.</summary>
    public static IServiceCollection AddApiCommon(this IServiceCollection services)
    {
        services.Configure<JsonOptions>(o =>
        {
            var json = o.SerializerOptions;
            json.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            // Enum ↔ chuỗi snake_case giống frontend ("test_scheduled", "branch")
            json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
            // Chống mass assignment: gửi trường lạ → 400 (request-pipeline.md mục 6)
            json.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            json.MaxDepth = 32;
            json.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Extensions.TryAdd("traceId", ctx.HttpContext.TraceIdentifier);
            ctx.ProblemDetails.Extensions.TryAdd("code", ctx.ProblemDetails.Status switch
            {
                StatusCodes.Status401Unauthorized => "auth.unauthenticated",
                StatusCodes.Status403Forbidden => "system.permission.forbidden",
                StatusCodes.Status404NotFound => "system.not_found",
                StatusCodes.Status405MethodNotAllowed => "system.method_not_allowed",
                _ => "system.error",
            });
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }
}
