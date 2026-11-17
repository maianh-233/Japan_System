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

    internal void Rename(string title) => Title = title;

    internal void SetOrder(int order) => Order = order;

    internal void Add(Material material) => _materials.Add(material);

    internal Material? Remove(MaterialId id)
    {
        var m = _materials.FirstOrDefault(x => x.Id == id);
        if (m is not null) _materials.Remove(m);
        return m;
    }

    internal Chapter CloneInto()
    {
        var c = new Chapter(Title, Order);
        foreach (var m in _materials) c._materials.Add(m.Clone());
        return c;
    }
}
