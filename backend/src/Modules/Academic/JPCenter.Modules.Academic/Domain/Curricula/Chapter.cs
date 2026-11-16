using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>Phần nhỏ trong bài (từ vựng, ngữ pháp, nghe...). Bảng: academic.chapters</summary>
public sealed class Chapter : Entity<ChapterId>
{
    private readonly List<Material> _materials = [];

    private Chapter() { }

    internal Chapter(string title, int order) : base(ChapterId.New()) => (Title, Order) = (title, order);

    public string Title { get; private set; } = null!;
    public int Order { get; private set; }
    public IReadOnlyList<Material> Materials => _materials;

    internal Material AddMaterial(string name, MaterialType type, string url, long? size, bool sharedWithStudents)
    {
        var m = new Material(name, type, url, size, sharedWithStudents);
        _materials.Add(m);
        return m;
    }

    internal Chapter CloneInto()
    {
        var c = new Chapter(Title, Order);
        foreach (var m in _materials) c._materials.Add(new Material(m.Name, m.Type, m.Url, m.Size, m.SharedWithStudents));
        return c;
    }
}
