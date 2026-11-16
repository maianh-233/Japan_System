using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>Tài liệu giảng dạy của chapter (pdf, ppt, audio, link Drive...). Bảng: academic.materials</summary>
public sealed class Material : Entity<MaterialId>
{
    private Material() { }

    internal Material(string name, MaterialType type, string url, long? size, bool sharedWithStudents) : base(MaterialId.New()) =>
        (Name, Type, Url, Size, SharedWithStudents) = (name, type, url, size, sharedWithStudents);

    public string Name { get; private set; } = null!;
    public MaterialType Type { get; private set; }
    /// <summary>Khóa file đã tải lên hoặc link ngoài</summary>
    public string Url { get; private set; } = null!;
    /// <summary>Byte — chỉ file tải lên</summary>
    public long? Size { get; private set; }
    /// <summary>true = HV xem được trên cổng; false = chỉ GV (giáo án, đáp án)</summary>
    public bool SharedWithStudents { get; private set; }
}
