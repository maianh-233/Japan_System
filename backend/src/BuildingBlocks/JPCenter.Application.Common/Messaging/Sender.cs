using System.Collections.Concurrent;
using JPCenter.SharedKernel.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.Application.Common.Messaging;

/// <summary>Hiện thực <see cref="ISender"/>: tìm handler theo kiểu request trong DI, bọc các behavior theo thứ tự đăng ký.</summary>
internal sealed class Sender(IServiceProvider services) : ISender
{
    private static readonly ConcurrentDictionary<Type, object> Invokers = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) where TResponse : Result
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoker = (IInvoker<TResponse>)Invokers.GetOrAdd(request.GetType(),
            t => Activator.CreateInstance(typeof(Invoker<,>).MakeGenericType(t, typeof(TResponse)))!);
        return invoker.Invoke(request, services, ct);
    }

    private interface IInvoker<TResponse> where TResponse : Result
    {
        Task<TResponse> Invoke(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct);
    }

    private sealed class Invoker<TRequest, TResponse> : IInvoker<TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : Result
    {
        public Task<TResponse> Invoke(IRequest<TResponse> request, IServiceProvider services, CancellationToken ct)
        {
            var typed = (TRequest)request;
            var handler = services.GetService<IRequestHandler<TRequest, TResponse>>()
                ?? throw new InvalidOperationException($"Chưa đăng ký handler cho {typeof(TRequest).Name}");

            Func<Task<TResponse>> next = () => handler.Handle(typed, ct);
            // Behavior đăng ký trước chạy ngoài cùng
            foreach (var behavior in services.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
            {
                var inner = next;
                next = () => behavior.Handle(typed, inner, ct);
            }
            return next();
        }
    }
}
