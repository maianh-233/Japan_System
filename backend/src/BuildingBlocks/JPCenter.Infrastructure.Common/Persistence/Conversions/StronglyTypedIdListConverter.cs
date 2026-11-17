using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JPCenter.Infrastructure.Common.Persistence.Conversions;

/// <summary>Danh sách Id strongly-typed (vd <c>List&lt;BranchId&gt;</c>) ↔ cột <c>uuid[]</c>.</summary>
public sealed class StronglyTypedIdListConverter<TId>() : ValueConverter<List<TId>, Guid[]>(ToArray(), FromArray())
    where TId : struct
{
    private static readonly Func<TId, Guid> Unwrap = new StronglyTypedIdConverter<TId>().ConvertToProviderTyped;
    private static readonly Func<Guid, TId> Wrap = new StronglyTypedIdConverter<TId>().ConvertFromProviderTyped;

    private static Expression<Func<List<TId>, Guid[]>> ToArray() => list => list.Select(x => Unwrap(x)).ToArray();

    private static Expression<Func<Guid[], List<TId>>> FromArray() => arr => arr.Select(x => Wrap(x)).ToList();
}

/// <summary>So sánh danh sách theo nội dung để EF phát hiện thay đổi.</summary>
public sealed class ListValueComparer<T>() : ValueComparer<List<T>>(
    (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
    l => l.Aggregate(0, (h, x) => HashCode.Combine(h, x)),
    l => l.ToList());
