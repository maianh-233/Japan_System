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
    public Guid? DoneBy { get; private set; }
    public DateTimeOffset? DoneAt { get; private set; }

    internal void Set(bool done, Guid by, DateTimeOffset at) =>
        (Done, DoneBy, DoneAt) = done ? (true, (Guid?)by, (DateTimeOffset?)at) : (false, null, null);
}
