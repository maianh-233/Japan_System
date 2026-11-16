using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.TimeRecords;

/// <summary>Id bản ghi chấm công ngày</summary>
public readonly record struct TimeRecordId(Guid Value)
{
    public static TimeRecordId New() => new(IdGenerator.NewId());
}
