using System.Collections.Concurrent;
using JPCenter.SharedKernel.Events;

namespace JPCenter.Infrastructure.Common.Outbox;

/// <summary>Tra kiểu sự kiện theo tên đầy đủ lưu trong outbox (quét assembly JPCenter.* đã nạp; nạp thêm thì quét lại).</summary>
public static class IntegrationEventTypes
{
    private static readonly ConcurrentDictionary<string, Type> Known = new();

    public static Type? Find(string fullName)
    {
        if (Known.TryGetValue(fullName, out var type)) return type;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name?.StartsWith("JPCenter", StringComparison.Ordinal) == true))
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                types = ex.Types.OfType<Type>().ToArray();
            }
            foreach (var t in types.Where(t => !t.IsAbstract && typeof(IIntegrationEvent).IsAssignableFrom(t) && t.FullName is not null))
                Known.TryAdd(t.FullName!, t);
        }
        return Known.GetValueOrDefault(fullName);
    }
}
