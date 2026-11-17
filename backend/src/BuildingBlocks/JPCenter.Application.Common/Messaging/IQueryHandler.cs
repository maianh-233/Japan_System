using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Messaging;

/// <summary>Handler của truy vấn.</summary>
public interface IQueryHandler<in TQuery, TValue> : IRequestHandler<TQuery, Result<TValue>>
    where TQuery : IQuery<TValue>;
