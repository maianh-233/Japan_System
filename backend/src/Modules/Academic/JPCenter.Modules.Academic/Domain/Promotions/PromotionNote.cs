using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Promotions;

/// <summary>Ghi chú tư vấn lên lớp. Bảng: academic.promotion_notes</summary>
public sealed class PromotionNote
{
    private PromotionNote() { }

    internal PromotionNote(string text, string? channel, Guid by, string byName, DateTimeOffset at) =>
        (Id, Text, Channel, By, ByName, At) = (IdGenerator.NewId(), text, channel, by, byName, at);

    public Guid Id { get; private set; }
    public string Text { get; private set; } = null!;
    /// <summary>Kênh tư vấn: call, zalo, meeting, message, email</summary>
    public string? Channel { get; private set; }
    public Guid By { get; private set; }
    public string ByName { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
}
