using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>
/// Tài liệu giảng dạy của chapter. 2 dạng: <b>file</b> tải lên kho (<see cref="FileKey"/> — PDF, Word, PPT, Excel, audio; ADR 0003)
/// hoặc <b>link</b> ngoài (<see cref="Url"/> — video, Google Drive, web). Bảng: academic.materials
/// </summary>
public sealed class Material : Entity<MaterialId>
{
    private Material() { }

    private Material(string name, MaterialType type, string? url, string? fileKey, string? fileName, string? contentType, long? size,
        bool sharedWithStudents) : base(MaterialId.New()) =>
        (Name, Type, Url, FileKey, FileName, ContentType, Size, SharedWithStudents) =
        (name, type, url, fileKey, fileName, contentType, size, sharedWithStudents);

    public string Name { get; private set; } = null!;
    public MaterialType Type { get; private set; }
    /// <summary>Link ngoài — chỉ tài liệu dạng link</summary>
    public string? Url { get; private set; }
    /// <summary>Khóa file trong kho (IFileStorage) — chỉ tài liệu dạng file. Bản sao / phiên bản mới dùng chung key</summary>
    public string? FileKey { get; private set; }
    /// <summary>Tên tệp gốc (để tải về đúng tên)</summary>
    public string? FileName { get; private set; }
    public string? ContentType { get; private set; }
    /// <summary>Byte — chỉ file tải lên</summary>
    public long? Size { get; private set; }
    /// <summary>true = HV xem được trên cổng; false = chỉ GV (giáo án, đáp án)</summary>
    public bool SharedWithStudents { get; private set; }

    public bool IsFile => FileKey is not null;

    internal static Material File(string name, MaterialType type, string fileKey, string fileName, string contentType, long size,
        bool sharedWithStudents) => new(name, type, null, fileKey, fileName, contentType, size, sharedWithStudents);

    internal static Material Link(string name, MaterialType type, string url, bool sharedWithStudents) =>
        new(name, type, url, null, null, null, null, sharedWithStudents);

    internal void Update(string name, bool sharedWithStudents) => (Name, SharedWithStudents) = (name, sharedWithStudents);

    internal Material Clone() => new(Name, Type, Url, FileKey, FileName, ContentType, Size, SharedWithStudents);
}
