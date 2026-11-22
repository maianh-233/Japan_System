namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>Trạng thái kỳ thi: nháp (đổi được đề) → đã phát hành (đề chốt) · đã hủy</summary>
public enum ClassExamStatus
{
    Draft,
    Published,
    Cancelled,
}
