using JPCenter.Application.Common.IntegrationEvents;
using System.Reflection;
using FluentValidation;
using JPCenter.Application.Common.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JPCenter.Application.Common.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>Đăng ký ISender + pipeline chung (Log → Validate). Gọi 1 lần ở Host.</summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.TryAddScoped<ISender, Sender>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));
        return services;
    }

    /// <summary>
    /// Quét assembly của module: đăng ký mọi handler (command / query, integration event) + validator.
    /// Mỗi module gọi cho chính assembly của mình.
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        Type[] handlerInterfaces = [typeof(IRequestHandler<,>), typeof(IIntegrationEventHandler<>)];
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }))
        {
            foreach (var itf in type.GetInterfaces().Where(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
                services.AddScoped(itf, type);
        }
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);
        return services;
    }
}
