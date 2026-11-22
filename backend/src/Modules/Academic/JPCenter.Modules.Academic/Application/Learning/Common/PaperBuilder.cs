using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Learning.Common;

/// <summary>1 câu đã phát: phần thi + thứ tự đáp án hiển thị (vị trí → chỉ số gốc)</summary>
public sealed record PaperSlot(Guid QuestionId, int Section, IReadOnlyList<int> OptionOrder);

/// <summary>
/// Dựng đề cho HV từ danh sách câu đã phát (quiz bài tập, bài thi online): đảo đáp án theo thứ tự đã chốt, cấp link tạm file nghe; chấm & xem lại.
/// Đáp án chỉ đọc ở server — không đi kèm đề (quy tắc vàng 2). Câu đã ngừng dùng / xóa vẫn đọc (đề cũ giữ nguyên).
/// </summary>
public sealed class PaperBuilder(IAcademicDbContext db, LearningFiles files)
{
    /// <summary>Thứ tự đáp án: đảo ngẫu nhiên theo <paramref name="rng"/> hoặc giữ nguyên</summary>
    public static IReadOnlyList<int> Order(int optionCount, Random? rng) =>
        rng is null ? Enumerable.Range(0, optionCount).ToList() : Enumerable.Range(0, optionCount).OrderBy(_ => rng.Next()).ToList();

    public async Task<IReadOnlyList<PaperQuestionDto>> PaperAsync(IReadOnlyList<PaperSlot> slots, CancellationToken ct)
    {
        var questions = await LoadAsync(slots, ct);
        var result = new List<PaperQuestionDto>();
        foreach (var s in slots)
        {
            if (!questions.TryGetValue(s.QuestionId, out var q)) continue;
            result.Add(new PaperQuestionDto(q.Id.Value, s.Section, QuestionBank.SkillText(q.Skill), QuestionBank.Text(q.Format), q.Stem, q.Passage,
                await files.AudioUrlAsync(q.AudioKey, ct), q.AudioDurationSec, Reorder(q.Options, s.OptionOrder)));
        }
        return result;
    }

    /// <summary>
    /// Chấm: <paramref name="chosen"/> = câu → chỉ số đáp án GỐC đã chọn. Ghi lượt làm vào thống kê câu hỏi (NH-10, câu bỏ trống = -1) —
    /// gọi trước <c>SaveChangesAsync</c>. Trả bảng xem lại theo thứ tự hiển thị.
    /// </summary>
    public async Task<IReadOnlyList<ReviewQuestionDto>> GradeAsync(IReadOnlyList<PaperSlot> slots, IReadOnlyDictionary<Guid, int> chosen,
        bool recordStats, CancellationToken ct)
    {
        var questions = await LoadAsync(slots, ct, tracked: recordStats);
        var review = new List<ReviewQuestionDto>();
        foreach (var s in slots)
        {
            if (!questions.TryGetValue(s.QuestionId, out var q)) continue;
            int? pick = chosen.TryGetValue(s.QuestionId, out var c) ? c : null;
            if (recordStats) q.RecordAnswer(pick ?? -1);
            var order = s.OptionOrder.ToList();
            review.Add(new ReviewQuestionDto(q.Id.Value, s.Section, q.Stem, q.Passage, Reorder(q.Options, order),
                pick is { } p ? order.IndexOf(p) : null, order.IndexOf(q.Answer), pick == q.Answer, q.Explanation));
        }
        return review;
    }

    private async Task<Dictionary<Guid, Question>> LoadAsync(IReadOnlyList<PaperSlot> slots, CancellationToken ct, bool tracked = false)
    {
        var ids = slots.Select(s => new QuestionId(s.QuestionId)).Distinct().ToList();
        var q = db.Questions.IgnoreQueryFilters().Where(x => ids.Contains(x.Id));
        if (!tracked) q = q.AsNoTracking();
        return await q.ToDictionaryAsync(x => x.Id.Value, ct);
    }

    private static List<string> Reorder(IReadOnlyList<string> options, IReadOnlyList<int> order) =>
        order.Where(i => i >= 0 && i < options.Count).Select(i => options[i]).ToList();
}
