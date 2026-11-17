using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JPCenter.Infrastructure.Common.Persistence.Conversions;

/// <summary>Id strongly-typed (<c>record struct XxxId(Guid Value)</c>) ↔ cột <c>uuid</c>.</summary>
public sealed class StronglyTypedIdConverter<TId>() : ValueConverter<TId, Guid>(ToGuid(), FromGuid())
    where TId : struct
{
    private static Expression<Func<TId, Guid>> ToGuid()
    {
        var id = Expression.Parameter(typeof(TId), "id");
        return Expression.Lambda<Func<TId, Guid>>(Expression.Property(id, "Value"), id);
    }

    private static Expression<Func<Guid, TId>> FromGuid()
    {
        var value = Expression.Parameter(typeof(Guid), "value");
        var ctor = typeof(TId).GetConstructor([typeof(Guid)])!;
        return Expression.Lambda<Func<Guid, TId>>(Expression.New(ctor, value), value);
    }
}
