using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>
/// Bài test xếp lớp: bốc N câu ĐÃ DUYỆT từ Ngân hàng câu hỏi theo trình độ + kỹ năng; thang điểm gợi ý trình độ / khóa.
/// Bảng: crm.placement_tests
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
    /// <summary>Thang gợi ý theo % điểm (jsonb)</summary>
    public List<PlacementBand> Bands { get; private set; } = [];
    public PlacementTestStatus Status { get; private set; }
    /// <summary>Người soạn (Hr.Employee)</summary>
    public Guid AuthorId { get; private set; }

    public static Result<PlacementTest> Create(string code, string name, string? description, List<PlacementSection> sections,
        int durationMinutes, List<PlacementBand> bands, Guid authorId)
    {
        if (sections.Count == 0) return Error.Validation("crm.placement_test.no_section", "Bài test cần ít nhất 1 phần");
        if (durationMinutes <= 0) return Error.Validation("crm.placement_test.invalid_duration", "Thời gian làm bài phải lớn hơn 0");
        return new PlacementTest
        {
            Id = PlacementTestId.New(), Code = code, Name = name, Description = description, Sections = sections,
            DurationMinutes = durationMinutes, Bands = bands, AuthorId = authorId, Status = PlacementTestStatus.Draft,
        };
    }

    public void SetQuestions(List<Guid> questionIds) => QuestionIds = questionIds;

    public Result Activate()
    {
        if (QuestionIds.Count == 0) return Error.Conflict("crm.placement_test.no_question", "Chưa bốc câu hỏi cho bài test");
        Status = PlacementTestStatus.Active;
        return Result.Success();
    }

    public void Archive() => Status = PlacementTestStatus.Archived;

    /// <summary>Gợi ý theo % điểm: band có MinPct cao nhất mà điểm đạt tới</summary>
    public PlacementBand? Suggest(decimal percent) =>
        Bands.Where(b => percent >= b.MinPct).OrderByDescending(b => b.MinPct).FirstOrDefault();
}
