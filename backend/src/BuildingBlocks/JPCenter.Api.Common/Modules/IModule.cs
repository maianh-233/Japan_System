using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Api.Common.Modules;

/// <summary>
/// Điểm vào của 1 module: tự đăng ký dịch vụ + endpoint. Host chỉ liệt kê module (<c>AddModules</c> / <c>MapModules</c>).
/// Hiện thực: <c>&lt;Module&gt;Module.cs</c> ở gốc project module (docs/module-template.md).
/// </summary>
public interface IModule
{
    /// <summary>Tên module = schema DB = đoạn đường dẫn API, vd "system" → /api/v1/system</summary>
    string Name { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Khai báo endpoint trên group <c>/api/v1/{Name}</c>.</summary>
    void MapEndpoints(IEndpointRouteBuilder group);
}
