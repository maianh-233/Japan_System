using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>Ghi chú về ứng viên. Bảng: hr.candidate_notes</summary>
public sealed class CandidateNote
{
    private CandidateNote() { }

    internal CandidateNote(string text, Guid by, string byName, DateTimeOffset at) => (Id, Text, By, ByName, At) = (IdGenerator.NewId(), text, by, byName, at);

    public Guid Id { get; private set; }
    public string Text { get; private set; } = null!;
    public Guid By { get; private set; }
    public string ByName { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
}
