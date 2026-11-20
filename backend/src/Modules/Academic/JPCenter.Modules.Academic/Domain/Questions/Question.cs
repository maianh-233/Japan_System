using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Questions;

/// <summary>
/// Câu hỏi trong Ngân hàng câu hỏi (user story 11) — nguồn DUY NHẤT cho quiz, kỳ thi, test xếp lớp. Chỉ câu Approved mới được bốc vào đề.
/// Dùng chung toàn trung tâm (không thuộc chi nhánh). Nội dung hỗ trợ furigana 漢字{かんじ}. Bảng: academic.questions
/// <code>
/// draft ──Gửi duyệt──▶ review ──Duyệt──▶ approved ──Ngừng dùng──▶ retired ──Mở lại──▶ review
///                        └──Trả lại──▶ draft          (sửa câu approved → review)
/// </code>
/// </summary>
public sealed class Question : AggregateRoot<QuestionId>, IAuditable, ISoftDeletable
{
    public const int MaxAudioSeconds = 1800;

    private Question() { }

    public string Code { get; private set; } = null!;
    public JlptLevel Level { get; private set; }
    public JapaneseSkill Skill { get; private set; }
    public QuestionFormat Format { get; private set; }
    public QuestionDifficulty Difficulty { get; private set; }
    /// <summary>Đề bài (furigana, chỗ trống ＿＿, vị trí ★)</summary>
    public string Stem { get; private set; } = null!;
    /// <summary>Đoạn văn đọc hiểu (nhiều câu dùng chung)</summary>
    public string? Passage { get; private set; }
    /// <summary>File nghe (khóa trong kho file)</summary>
    public string? AudioKey { get; private set; }
    /// <summary>Tên file gốc (hiển thị)</summary>
    public string? AudioName { get; private set; }
    public int? AudioDurationSec { get; private set; }
    /// <summary>Lời thoại — chỉ GV / nhân viên xem</summary>
    public string? Transcript { get; private set; }
    public List<string> Options { get; private set; } = [];
    /// <summary>Vị trí đáp án đúng (0-based) — không trả về cổng HV trước khi nộp</summary>
    public int Answer { get; private set; }
    public string? Explanation { get; private set; }
    public CurriculumId? CurriculumId { get; private set; }
    /// <summary>Gắn bài học để bốc câu theo bài</summary>
    public List<LessonId> LessonIds { get; private set; } = [];
    public List<string> Tags { get; private set; } = [];
    public QuestionStatus Status { get; private set; }
    /// <summary>Người soạn (Hr.Employee)</summary>
    public Guid AuthorId { get; private set; }
    /// <summary>Người duyệt / trả lại / ngừng dùng gần nhất (Hr.Employee)</summary>
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    /// <summary>Lý do trả lại / ngừng dùng</summary>
    public string? ReviewNote { get; private set; }
    /// <summary>Thống kê: số đề đã dùng, lượt làm, lượt đúng, số lần chọn từng đáp án</summary>
    public int UsedCount { get; private set; }
    public int AttemptCount { get; private set; }
    public int CorrectCount { get; private set; }
    public List<int> Picks { get; private set; } = [];

    public bool HasAudio => AudioKey is not null;

    /// <summary>NH-02 — tạo câu nháp; <paramref name="submit"/> = gửi duyệt luôn (kiểm tra như <see cref="Submit"/>).</summary>
    public static Result<Question> Create(string code, QuestionContent content, Guid authorId, bool submit = false)
    {
        var normalized = content.Normalize();
        if (normalized.IsFailure) return normalized.Error;

        var q = new Question { Id = QuestionId.New(), Code = code, AuthorId = authorId, Status = QuestionStatus.Draft };
        q.Apply(normalized.Value);
        q.Picks = [.. new int[QuestionContent.OptionCount]];
        if (submit && q.Submit() is { IsFailure: true } r) return r.Error;
        return q;
    }

    /// <summary>
    /// NH-03 — sửa nội dung. Câu ngừng dùng không sửa được; câu đã duyệt → chờ duyệt. Đổi đáp án → đặt lại thống kê chọn đáp án.
    /// Đổi khỏi kỹ năng nghe → bỏ file nghe (trả khóa file cũ để xóa trong kho qua <paramref name="removedAudioKey"/>).
    /// </summary>
    public Result Edit(QuestionContent content, out string? removedAudioKey)
    {
        removedAudioKey = null;
        if (Status == QuestionStatus.Retired) return QuestionErrors.Retired;
        var normalized = content.Normalize();
        if (normalized.IsFailure) return normalized.Error;

        var oldOptions = Options;
        Apply(normalized.Value);
        if (!oldOptions.SequenceEqual(Options)) Picks = [.. new int[QuestionContent.OptionCount]];
        if (Skill != JapaneseSkill.Listening && AudioKey is not null)
        {
            removedAudioKey = AudioKey;
            (AudioKey, AudioName, AudioDurationSec) = (null, null, null);
        }
        if (Status == QuestionStatus.Approved) Status = QuestionStatus.Review;
        return Result.Success();
    }

    /// <summary>NH-04 — gắn / thay file nghe. Trả khóa file cũ (nếu có) để xóa trong kho.</summary>
    public Result<string?> SetAudio(string audioKey, string audioName, int durationSec)
    {
        if (Status == QuestionStatus.Retired) return QuestionErrors.Retired;
        if (Skill != JapaneseSkill.Listening) return QuestionErrors.AudioNotListening;
        if (durationSec is < 1 or > MaxAudioSeconds) return QuestionErrors.AudioDuration;
        var old = AudioKey == audioKey ? null : AudioKey;
        (AudioKey, AudioName, AudioDurationSec) = (audioKey, audioName, durationSec);
        if (Status == QuestionStatus.Approved) Status = QuestionStatus.Review;
        return old;
    }

    /// <summary>NH-05 — gửi duyệt từ nháp. Nghe hiểu phải có file nghe (NH-02.E5).</summary>
    public Result Submit()
    {
        if (Status != QuestionStatus.Draft) return QuestionErrors.InvalidStatus;
        if (Skill == JapaneseSkill.Listening && AudioKey is null) return QuestionErrors.AudioRequired;
        Status = QuestionStatus.Review;
        return Result.Success();
    }

    /// <summary>NH-05 — duyệt (người soạn không tự duyệt)</summary>
    public Result Approve(Guid by, DateTimeOffset at) => Decide(QuestionStatus.Review, QuestionStatus.Approved, by, at, null, checkAuthor: true);

    /// <summary>NH-05 — trả lại cho người soạn sửa, bắt buộc lý do</summary>
    public Result Return(Guid by, DateTimeOffset at, string note) =>
        string.IsNullOrWhiteSpace(note) ? QuestionErrors.NoteRequired : Decide(QuestionStatus.Review, QuestionStatus.Draft, by, at, note.Trim(), checkAuthor: true);

    /// <summary>NH-06 — ngừng dùng câu đã duyệt (lộ đề, sai sót), bắt buộc lý do</summary>
    public Result Retire(Guid by, DateTimeOffset at, string note) =>
        string.IsNullOrWhiteSpace(note) ? QuestionErrors.NoteRequired : Decide(QuestionStatus.Approved, QuestionStatus.Retired, by, at, note.Trim(), checkAuthor: false);

    /// <summary>NH-06 — mở lại câu ngừng dùng → chờ duyệt lại</summary>
    public Result Reopen()
    {
        if (Status != QuestionStatus.Retired) return QuestionErrors.InvalidStatus;
        Status = QuestionStatus.Review;
        return Result.Success();
    }

    /// <summary>NH-07 — bản sao nháp (dùng chung file nghe), thống kê = 0</summary>
    public Question Duplicate(string code, Guid authorId)
    {
        var copy = new Question
        {
            Id = QuestionId.New(), Code = code, AuthorId = authorId, Status = QuestionStatus.Draft,
            AudioKey = AudioKey, AudioName = AudioName, AudioDurationSec = AudioDurationSec,
            Picks = [.. new int[QuestionContent.OptionCount]],
        };
        copy.Apply(new QuestionContent(Level, Skill, Format, Difficulty, Stem, Passage, Transcript, [.. Options], Answer, Explanation, CurriculumId,
            [.. LessonIds], [.. Tags]));
        return copy;
    }

    /// <summary>NH-09 — được bốc vào 1 đề</summary>
    public void RecordUse() => UsedCount++;

    /// <summary>NH-10 — 1 lượt học viên làm câu này (picked = vị trí đã chọn; ngoài khoảng = bỏ trống)</summary>
    public void RecordAnswer(int picked)
    {
        AttemptCount++;
        if (picked == Answer) CorrectCount++;
        if (Picks.Count != Options.Count) Picks = [.. new int[Options.Count]];
        if (picked >= 0 && picked < Picks.Count) Picks[picked]++;
    }

    /// <summary>Tỷ lệ đúng (%) — null khi chưa có lượt làm</summary>
    public int? CorrectRate => AttemptCount == 0 ? null : (int)Math.Round(CorrectCount * 100.0 / AttemptCount);

    private void Apply(QuestionContent c)
    {
        (Level, Skill, Format, Difficulty, Stem, Passage, Transcript, Answer, Explanation, CurriculumId) =
            (c.Level, c.Skill, c.Format, c.Difficulty, c.Stem, c.Passage, c.Transcript, c.Answer, c.Explanation, c.CurriculumId);
        Options = [.. c.Options];
        LessonIds = [.. c.LessonIds];
        Tags = [.. c.Tags];
    }

    private Result Decide(QuestionStatus from, QuestionStatus to, Guid by, DateTimeOffset at, string? note, bool checkAuthor)
    {
        if (Status != from) return QuestionErrors.InvalidStatus;
        if (checkAuthor && by == AuthorId) return QuestionErrors.SelfReview;
        (Status, ReviewedBy, ReviewedAt, ReviewNote) = (to, by, at, note);
        return Result.Success();
    }
}
