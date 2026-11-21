using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>
/// Bài test xếp lớp: bốc N câu ĐÃ DUYỆT từ Ngân hàng câu hỏi theo trình độ + kỹ năng (<see cref="PlacementDraw"/>);
/// thang điểm gợi ý trình độ / khóa. Danh sách câu CỐ ĐỊNH khi lưu (CRM-12.E5). Bảng: crm.placement_tests
/// </summary>
public sealed class PlacementTest : AggregateRoot<PlacementTestId>, IAuditable, ISoftDeletable
{
    private PlacementTest() { }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    /// <summary>Cấu trúc đề (jsonb)</summary>
    public List<PlacementSection> Sections { get; private set; } = [];
    /// <summary>Câu đã bốc, cố định cho bài test (Academic.Question) — đổi đề bằng "Bốc lại câu"</summary>
    public List<Guid> QuestionIds { get; private set; } = [];
    public int DurationMinutes { get; private set; }
    /// <summary>Thang gợi ý theo % điểm (jsonb), xếp mốc tăng dần</summary>
    public List<PlacementBand> Bands { get; private set; } = [];
    public PlacementTestStatus Status { get; private set; }
    /// <summary>Người soạn (Hr.Employee)</summary>
    public Guid AuthorId { get; private set; }

    /// <summary>CRM-12 — soạn bài test (nháp). <paramref name="questionIds"/> = kết quả <see cref="PlacementDraw.Draw"/>.</summary>
    public static Result<PlacementTest> Create(string code, string name, string? description, IReadOnlyList<PlacementSection> sections,
        int durationMinutes, IReadOnlyList<PlacementBand> bands, IReadOnlyList<Guid> questionIds, Guid authorId)
    {
        var test = new PlacementTest { Id = PlacementTestId.New(), Code = code, AuthorId = authorId, Status = PlacementTestStatus.Draft };
        var info = test.UpdateInfo(name, description, bands);
        if (info.IsFailure) return info.Error;
        var structure = test.Restructure(sections, durationMinutes, questionIds, hasAttempts: false);
        return structure.IsFailure ? structure.Error : test;
    }

    /// <summary>Sửa tên / mô tả / thang — luôn được, kể cả khi đã có người làm (CRM-12.E6)</summary>
    public Result UpdateInfo(string name, string? description, IReadOnlyList<PlacementBand> bands)
    {
        if (string.IsNullOrWhiteSpace(name)) return PlacementTestErrors.NameRequired;
        if (bands.Count == 0 || bands.All(b => b.MinPct != 0)) return PlacementTestErrors.BandZeroRequired;
        if (bands.Any(b => b.MinPct is < 0 or > 100 || string.IsNullOrWhiteSpace(b.Level))
            || bands.Select(b => b.MinPct).Distinct().Count() != bands.Count) return PlacementTestErrors.InvalidBand;

        (Name, Description) = (name.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim());
        Bands = bands.Select(b => b with { Level = b.Level.Trim(), Course = b.Course.Trim() }).OrderBy(b => b.MinPct).ToList();
        return Result.Success();
    }

    /// <summary>
    /// Đổi cấu trúc / thời gian / câu hỏi ("Đổi đề"). Đã có người làm → từ chối, tạo bài test mới (CRM-12.E6).
    /// </summary>
    public Result Restructure(IReadOnlyList<PlacementSection> sections, int durationMinutes, IReadOnlyList<Guid> questionIds, bool hasAttempts)
    {
        if (hasAttempts) return PlacementTestErrors.HasAttempts;
        if (sections.Count == 0) return PlacementTestErrors.NoSection;
        if (sections.Any(s => s.Count <= 0 || s.Level == JlptLevel.Beginner)) return PlacementTestErrors.InvalidSection;
        if (durationMinutes <= 0 || durationMinutes > PlacementTestErrors.MaxDurationMinutes) return PlacementTestErrors.InvalidDuration;
        if (questionIds.Count == 0) return PlacementTestErrors.NoQuestion;

        (Sections, DurationMinutes, QuestionIds) = (sections.ToList(), durationMinutes, questionIds.Distinct().ToList());
        return Result.Success();
    }

    /// <summary>Nháp / lưu trữ → Đang dùng (Sale gửi link được)</summary>
    public Result Activate()
    {
        if (QuestionIds.Count == 0) return PlacementTestErrors.NoQuestion;
        Status = PlacementTestStatus.Active;
        return Result.Success();
    }

    /// <summary>Lưu trữ — không gửi link mới; lượt đã gửi vẫn làm được tới hạn</summary>
    public void Archive() => Status = PlacementTestStatus.Archived;

    /// <summary>Gợi ý theo % điểm: mốc cao nhất mà điểm đạt tới</summary>
    public PlacementBand? Suggest(decimal percent) =>
        Bands.Where(b => percent >= b.MinPct).OrderByDescending(b => b.MinPct).FirstOrDefault();
}
