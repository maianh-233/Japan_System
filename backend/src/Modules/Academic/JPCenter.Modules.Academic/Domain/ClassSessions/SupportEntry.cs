using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Nhật ký hỗ trợ của nhân viên trong buổi. Bảng: academic.session_support_entries</summary>
public sealed class SupportEntry
{
    private SupportEntry() { }

    internal SupportEntry(SupportKind kind, string note, Guid by, DateTimeOffset at) => (Id, Kind, Note, By, At) = (IdGenerator.NewId(), kind, note, by, at);

    public Guid Id { get; private set; }
    public SupportKind Kind { get; private set; }
    public string Note { get; private set; } = null!;
    public Guid By { get; private set; }
    public DateTimeOffset At { get; private set; }
}
