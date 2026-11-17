using System.Collections;
using System.Globalization;
using JPCenter.Application.Common.Auditing;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JPCenter.Infrastructure.Common.Auditing;

/// <summary>
/// Ghi lại trước → sau của entity <see cref="IAuditable"/> khi lưu, đưa vào <see cref="IAuditChangeCollector"/>
/// để gắn vào dòng nhật ký của hành động (filter <c>.Audited</c>). Giá trị bí mật (mật khẩu, token, khóa) luôn bị che.
/// </summary>
public sealed class DataChangeAuditInterceptor(IAuditChangeCollector collector) : SaveChangesInterceptor
{
    private static readonly string[] SecretMarkers = ["password", "secret", "token", "hash", "otp", "key"];
    /// <summary>Số tài khoản ngân hàng: chỉ ghi 4 số cuối (••••1234) — đủ để nhận ra đã đổi, không lộ số đầy đủ</summary>
    private static readonly string[] PartialMarkers = ["bankaccount", "accountnumber"];

    private static readonly HashSet<string> CommonColumns =
    [
        ShadowColumns.CreatedAt, ShadowColumns.CreatedBy, ShadowColumns.UpdatedAt, ShadowColumns.UpdatedBy,
        ShadowColumns.DeletedAt, ShadowColumns.DeletedBy, ShadowColumns.RowVersion,
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Collect(DbContext? context)
    {
        if (context is null) return;
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IAuditable && !IsOwnedByAuditable(entry)) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            var name = entry.Metadata.ClrType.Name;
            foreach (var prop in entry.Properties)
            {
                var meta = prop.Metadata;
                if (meta.IsPrimaryKey() || CommonColumns.Contains(meta.Name)) continue;
                if (entry.State == EntityState.Modified && !prop.IsModified) continue;

                var before = entry.State == EntityState.Added ? null : Format(prop, prop.OriginalValue);
                var after = Format(prop, prop.CurrentValue);
                if (before == after) continue;
                if (IsSecret(meta.Name)) (before, after) = (before is null ? null : "***", after is null ? null : "***");
                else if (IsPartial(meta.Name)) (before, after) = (Last4(before), Last4(after));

                collector.Add(new AuditFieldChange($"{name}.{meta.Name}", before, after));
            }
        }
    }

    private static bool IsOwnedByAuditable(EntityEntry entry) => entry.Metadata.IsOwned();

    private static bool IsPartial(string property) =>
        PartialMarkers.Any(m => property.Contains(m, StringComparison.OrdinalIgnoreCase));

    public static string? Last4(string? value) => value is null ? null : "••••" + (value.Length > 4 ? value[^4..] : value);

    private static bool IsSecret(string property) =>
        SecretMarkers.Any(m => property.Contains(m, StringComparison.OrdinalIgnoreCase));

    private static string? Format(PropertyEntry prop, object? value)
    {
        if (value is null) return null;
        var converter = prop.Metadata.GetValueConverter() ?? prop.Metadata.GetTypeMapping().Converter;
        var raw = converter is null ? value : converter.ConvertToProvider(value);
        return raw switch
        {
            null => null,
            string s => s,
            DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            IEnumerable list => string.Join(", ", list.Cast<object?>().Select(x => x?.ToString())),
            _ => raw.ToString(),
        };
    }
}
