using System.Linq.Expressions;
using System.Reflection;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Infrastructure.Common.Persistence;

/// <summary>
/// DbContext gốc của mọi module (CLAUDE.md mục 7):
/// schema riêng · Id strongly-typed ↔ uuid · enum ↔ chuỗi snake_case · Money ↔ numeric(18,0) ·
/// cột chung created/updated (IAuditable) · xóa mềm + filter (ISoftDeletable) · xmin chống sửa đè cho aggregate.
/// Tên bảng / cột snake_case do EFCore.NamingConventions (đăng ký ở <see cref="PersistenceServiceCollectionExtensions"/>).
/// </summary>
public abstract class ModuleDbContextBase(DbContextOptions options) : DbContext(options)
{
    /// <summary>Tên schema của module, vd "system".</summary>
    protected abstract string Schema { get; }

    /// <summary>Assembly chứa entity + cấu hình của module.</summary>
    private Assembly ModuleAssembly => GetType().Assembly;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        foreach (var asm in new[] { ModuleAssembly, typeof(BranchId).Assembly }.Distinct())
        {
            foreach (var type in asm.GetTypes())
            {
                if (IsStronglyTypedId(type))
                {
                    builder.Properties(type).HaveConversion(typeof(StronglyTypedIdConverter<>).MakeGenericType(type));
                    var list = typeof(List<>).MakeGenericType(type);
                    builder.Properties(list).HaveConversion(
                        typeof(StronglyTypedIdListConverter<>).MakeGenericType(type),
                        typeof(ListValueComparer<>).MakeGenericType(type));
                }
                else if (type.IsEnum && type.IsPublic)
                {
                    builder.Properties(type).HaveConversion(typeof(SnakeCaseEnumConverter<>).MakeGenericType(type));
                    builder.Properties(typeof(List<>).MakeGenericType(type)).HaveConversion(
                        typeof(EnumListConverter<>).MakeGenericType(type),
                        typeof(ListValueComparer<>).MakeGenericType(type));
                }
            }
        }

        builder.Properties<Money>().HaveConversion<MoneyConverter>().HavePrecision(18, 0);
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(ModuleAssembly, t => t.Namespace?.Contains(".Infrastructure.") == true);

        // Chỉ entity thật (kế thừa Entity<>) — KHÔNG đụng tới kiểu EF tự phát hiện tạm thời (record lồng sẽ thành cột / jsonb),
        // gọi modelBuilder.Entity() lên chúng sẽ vô tình biến chúng thành bảng.
        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(e => !e.IsOwned() && IsDomainEntity(e.ClrType)).ToList())
        {
            var clr = entity.ClrType;
            var b = modelBuilder.Entity(clr);

            if (typeof(IAuditable).IsAssignableFrom(clr))
            {
                b.Property<DateTimeOffset>(ShadowColumns.CreatedAt);
                b.Property<Guid?>(ShadowColumns.CreatedBy);
                b.Property<DateTimeOffset?>(ShadowColumns.UpdatedAt);
                b.Property<Guid?>(ShadowColumns.UpdatedBy);
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clr))
            {
                b.Property<bool>(ShadowColumns.IsDeleted).HasDefaultValue(false);
                b.Property<DateTimeOffset?>(ShadowColumns.DeletedAt);
                b.Property<Guid?>(ShadowColumns.DeletedBy);

                var e = Expression.Parameter(clr, "e");
                var notDeleted = Expression.Not(Expression.Call(typeof(EF), nameof(EF.Property), [typeof(bool)], e,
                    Expression.Constant(ShadowColumns.IsDeleted)));
                b.HasQueryFilter(ShadowColumns.SoftDeleteFilter, Expression.Lambda(notDeleted, e));
            }

            if (IsAggregateRoot(clr))
                b.Property<uint>(ShadowColumns.RowVersion).IsRowVersion();

            // Aggregate root giữ domain event trong bộ nhớ — không lưu
            if (clr.GetProperty("DomainEvents") is not null) b.Ignore("DomainEvents");
        }
    }

    private static bool IsStronglyTypedId(Type t) =>
        t is { IsValueType: true, IsEnum: false, IsGenericType: false }
        && t.Name.EndsWith("Id", StringComparison.Ordinal)
        && t.GetProperty("Value")?.PropertyType == typeof(Guid)
        && t.GetConstructor([typeof(Guid)]) is not null;

    private static bool IsDomainEntity(Type t)
    {
        for (var cur = t.BaseType; cur is not null; cur = cur.BaseType)
            if (cur.IsGenericType && cur.GetGenericTypeDefinition() == typeof(Entity<>)) return true;
        return false;
    }

    private static bool IsAggregateRoot(Type t)
    {
        for (var cur = t.BaseType; cur is not null; cur = cur.BaseType)
            if (cur.IsGenericType && cur.GetGenericTypeDefinition() == typeof(AggregateRoot<>)) return true;
        return false;
    }
}
