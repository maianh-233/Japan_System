using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Abroad;

/// <summary>1 giấy tờ trong checklist hồ sơ du học. Bảng: students.abroad_documents</summary>
public sealed class AbroadDocument
{
    private AbroadDocument() { }

    internal AbroadDocument(string name) => (Id, Name, State) = (IdGenerator.NewId(), name, DocumentState.Missing);

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public DocumentState State { get; private set; }

    internal void SetState(DocumentState state) => State = state;
}
