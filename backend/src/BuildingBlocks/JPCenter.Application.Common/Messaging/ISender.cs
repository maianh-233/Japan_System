using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Messaging;

/// <summary>Gửi Command / Query tới đúng handler, chạy qua pipeline (thay MediatR).</summary>
public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) where TResponse : Result;
}
