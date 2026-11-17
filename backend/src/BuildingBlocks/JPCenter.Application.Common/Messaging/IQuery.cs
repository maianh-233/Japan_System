using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Messaging;

/// <summary>Truy vấn chỉ đọc.</summary>
public interface IQuery<TValue> : IRequest<Result<TValue>>;
