using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Messaging;

/// <summary>Xử lý 1 loại yêu cầu. Viết handler qua <see cref="ICommandHandler{TCommand}"/> / <see cref="IQueryHandler{TQuery, TValue}"/>.</summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    Task<TResponse> Handle(TRequest request, CancellationToken ct);
}
