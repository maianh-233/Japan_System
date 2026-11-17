using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;

namespace JPCenter.Api.Common.Validation;

/// <summary>Giới hạn kích thước body riêng cho 1 endpoint (mặc định toàn API: 1 MB).</summary>
public static class BodyLimitExtensions
{
    /// <summary>Vd endpoint nhập Excel: <c>.WithBodyLimit(5 * 1024 * 1024)</c>.</summary>
    public static TBuilder WithBodyLimit<TBuilder>(this TBuilder builder, long maxBytes) where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new BodyLimit(maxBytes));

    private sealed record BodyLimit(long? MaxRequestBodySize) : IRequestSizeLimitMetadata;
}
