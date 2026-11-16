using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Abroad;

/// <summary>Ghi chú tiến trình hồ sơ du học. Bảng: students.abroad_notes</summary>
public sealed class AbroadNote
{
    private AbroadNote() { }

    internal AbroadNote(string text, Guid by, string byName, DateTimeOffset at) => (Id, Text, By, ByName, At) = (IdGenerator.NewId(), text, by, byName, at);

    public Guid Id { get; private set; }
    public string Text { get; private set; } = null!;
    public Guid By { get; private set; }
    public string ByName { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
}
