namespace JPCenter.Modules.Work.Domain.Handovers;

/// <summary>Lead đang dở bàn giao cho ca sau. Bảng: work.handover_leads, khóa (handover_id, lead_id)</summary>
public sealed class HandoverLead
{
    private HandoverLead() { }

    internal HandoverLead(Guid leadId, string note) => (LeadId, Note) = (leadId, note);

    /// <summary>Crm.Lead</summary>
    public Guid LeadId { get; private set; }
    public string Note { get; private set; } = null!;
}
