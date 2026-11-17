using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Messaging;

/// <summary>Yêu cầu gửi qua <see cref="ISender"/>. Kết quả luôn là <see cref="Result"/> / <see cref="Result{T}"/>.</summary>
public interface IRequest<TResponse> where TResponse : Result;
