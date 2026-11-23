namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>1 việc trong checklist nhận việc. Bảng: hr.onboarding_items, khóa (onboarding_id, key)</summary>
public sealed class OnboardingItem
{
    private OnboardingItem() { }

    internal OnboardingItem(string key, OnboardingGroup group, string label) => (Key, Group, Label) = (key, group, label);

    public string Key { get; private set; } = null!;
    public OnboardingGroup Group { get; private set; }
    public string Label { get; private set; } = null!;
    public bool Done { get; private set; }
    /// <summary>Người tick (Hr.Employee); null khi hệ thống tự tick</summary>
    public Guid? DoneBy { get; private set; }
    public string? DoneByName { get; private set; }
    public DateTimeOffset? DoneAt { get; private set; }

    internal void Set(bool done, Guid by, string byName, DateTimeOffset at) =>
        (Done, DoneBy, DoneByName, DoneAt) = done ? (true, (Guid?)by, byName, (DateTimeOffset?)at) : (false, null, null, null);
}
