using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Crm.Domain.Leads;

/// <summary>1 lần tư vấn trong lịch sử tư vấn của lead. Bảng: crm.consultations</summary>
public sealed class Consultation : Entity<ConsultationId>
{
    private Consultation() { }

    internal Consultation(ConsultChannel channel, ConsultResult result, string content, Guid? staffId, string staffName,
        DateTimeOffset createdAt, DateTimeOffset? nextFollowUpAt, bool isSystem) : base(ConsultationId.New())
    {
        (Channel, Result, Content, StaffId, StaffName, CreatedAt, NextFollowUpAt, IsSystem) =
            (channel, result, content, staffId, staffName, createdAt, nextFollowUpAt, isSystem);
    }

    public ConsultChannel Channel { get; private set; }
    public ConsultResult Result { get; private set; }
    public string Content { get; private set; } = null!;
    /// <summary>Nhân viên tư vấn (Hr.Employee) — null với dòng hệ thống</summary>
    public Guid? StaffId { get; private set; }
    public string StaffName { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? NextFollowUpAt { get; private set; }
    /// <summary>Dòng do hệ thống tự sinh</summary>
    public bool IsSystem { get; private set; }
}
