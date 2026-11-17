namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>Quy tắc loại tài liệu: loại nào là file / link, đoán loại từ đuôi tệp (giống <c>materialTypeOf</c> ở frontend)</summary>
public static class MaterialTypes
{
    /// <summary>Video, Google Drive, Liên kết — dán đường dẫn, không tải lên</summary>
    public static bool IsLink(MaterialType type) => type is MaterialType.Video or MaterialType.Drive or MaterialType.Link;

    public static MaterialType FromFileName(string fileName) => Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant() switch
    {
        "doc" or "docx" => MaterialType.Doc,
        "ppt" or "pptx" => MaterialType.Ppt,
        "xls" or "xlsx" or "csv" => MaterialType.Xls,
        "mp3" or "wav" or "m4a" => MaterialType.Audio,
        _ => MaterialType.Pdf,
    };
}
