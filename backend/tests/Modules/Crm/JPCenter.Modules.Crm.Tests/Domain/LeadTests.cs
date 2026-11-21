using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.Leads.Events;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Tests.Domain;

public sealed class LeadTests
{
    private static readonly BranchId Branch = new(Guid.NewGuid());
    private static readonly Guid Sale = Guid.NewGuid(), Sale2 = Guid.NewGuid(), Kid = Guid.NewGuid(), Kid2 = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 11, 21, 2, 0, 0, TimeSpan.Zero);

    private static LeadInfo Info(string name = "Nguyễn Văn Bình", string phone = "0912345678", string? email = null) =>
        new(name, phone, email, LeadSource.Facebook, Branch, "N5 cấp tốc", null, null, null);

    private static Lead New(LeadStatus status = LeadStatus.New, string? lostReason = null) =>
        Lead.Create(Lead.CodeFor(12), Info(), status, lostReason).Value;

    [Fact]
    public void CRM_02_tao_lead_chuan_hoa_va_phat_su_kien()
    {
        var lead = Lead.Create("LD0001", Info("  Trần   Thị  Hoa ", "+84 912.345.678", " Hoa@Mail.COM ")).Value;
        lead.Code.ShouldBe("LD0001");
        lead.FullName.ShouldBe("Trần Thị Hoa");
        lead.Phone.ShouldBe("0912345678");
        lead.Email.ShouldBe("hoa@mail.com");
        lead.Status.ShouldBe(LeadStatus.New);
        lead.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<LeadCreated>();
        Lead.CodeFor(7).ShouldBe("LD0007");
    }

    [Fact]
    public void CRM_02_E1_E2_thieu_ten_sdt_sai_dinh_dang()
    {
        Lead.Create("x", Info(name: " ")).Error.ShouldBe(LeadErrors.NameRequired);
        Lead.Create("x", Info(phone: "")).Error.ShouldBe(LeadErrors.PhoneRequired);
        Lead.Create("x", Info(phone: "091234")).Error.ShouldBe(LeadErrors.InvalidPhone);
        Lead.Create("x", Info(phone: "1912345678")).Error.ShouldBe(LeadErrors.InvalidPhone);
        Lead.Create("x", Info(email: "abc")).Error.ShouldBe(LeadErrors.InvalidEmail);
    }

    [Fact]
    public void CRM_02_E4_tao_lead_mat_can_ly_do_va_khong_tao_da_chot()
    {
        Lead.Create("x", Info(), LeadStatus.Lost).Error.ShouldBe(LeadErrors.LostReasonRequired);
        Lead.Create("x", Info(), LeadStatus.Lost, " Học phí cao ").Value.LostReason.ShouldBe("Học phí cao");
        Lead.Create("x", Info(), LeadStatus.Won).Error.ShouldBe(LeadErrors.WonRequiresEnrollment);
    }

    [Fact]
    public void Giao_sale_phat_su_kien_khi_doi_nguoi()
    {
        var lead = New();
        lead.ClearDomainEvents();
        lead.AssignTo(Sale).ShouldBeTrue();
        lead.AssignTo(Sale).ShouldBeFalse();
        lead.AssignTo(Sale2).ShouldBeTrue();
        lead.AssigneeId.ShouldBe(Sale2);
        lead.DomainEvents.OfType<LeadAssigned>().Last().ShouldBe(new LeadAssigned(lead.Id, Sale2, Sale));
        lead.AssignTo(null).ShouldBeTrue();
        lead.DomainEvents.Count.ShouldBe(2);
    }

    [Fact]
    public void CRM_05_tu_van_dau_tien_chuyen_dang_tu_van_va_dung_sla()
    {
        var lead = New();
        lead.IsSlaOverdue(At.AddMinutes(-20), 15, At).ShouldBeTrue();

        lead.AddConsultation(ConsultChannel.Call, ConsultResult.Thinking, " Khách hỏi lịch tối ", Sale, "Khoa", At, At.AddDays(1))
            .IsSuccess.ShouldBeTrue();

        lead.Status.ShouldBe(LeadStatus.Consulting);
        lead.LastContactAt.ShouldBe(At);
        lead.NextFollowUpAt.ShouldBe(At.AddDays(1));
        lead.Consultations.ShouldHaveSingleItem().Content.ShouldBe("Khách hỏi lịch tối");
        lead.IsSlaOverdue(At.AddMinutes(-20), 15, At).ShouldBeFalse();
    }

    [Fact]
    public void CRM_05_E1_noi_dung_trong()
    {
        var lead = New();
        lead.AddConsultation(ConsultChannel.Call, ConsultResult.NoAnswer, "  ", Sale, "Khoa", At, null).Error
            .ShouldBe(LeadErrors.ConsultContentRequired);
        lead.Consultations.ShouldBeEmpty();
        lead.Status.ShouldBe(LeadStatus.New);
    }

    [Fact]
    public void CRM_05_E2_hen_test_chuyen_giai_doan()
    {
        var lead = New();
        lead.AddConsultation(ConsultChannel.Zalo, ConsultResult.TestBooked, "Hẹn test thứ 7", Sale, "Khoa", At, null);
        lead.Status.ShouldBe(LeadStatus.TestScheduled);
    }

    [Fact]
    public void Tu_van_khong_hen_moi_xoa_hen_cu_da_toi_han_giu_hen_tuong_lai()
    {
        var lead = New();
        lead.ScheduleFollowUp(At.AddHours(-1));
        lead.IsFollowUpOverdue(At).ShouldBeTrue();
        lead.AddConsultation(ConsultChannel.Call, ConsultResult.Interested, "Đã gọi lại", Sale, "Khoa", At, null);
        lead.NextFollowUpAt.ShouldBeNull();

        lead.ScheduleFollowUp(At.AddDays(2));
        lead.AddConsultation(ConsultChannel.Call, ConsultResult.Interested, "Nhắn thêm", Sale, "Khoa", At.AddMinutes(5), null);
        lead.NextFollowUpAt.ShouldBe(At.AddDays(2));
    }

    [Fact]
    public void Tu_van_lead_da_mat_khong_doi_giai_doan()
    {
        var lead = New(LeadStatus.Lost, "Không liên lạc được");
        lead.AddConsultation(ConsultChannel.Call, ConsultResult.TestBooked, "Khách gọi lại", Sale, "Khoa", At, null);
        lead.Status.ShouldBe(LeadStatus.Lost);
    }

    [Fact]
    public void CRM_04_E1_E2_mat_can_ly_do_khong_keo_sang_da_chot()
    {
        var lead = New();
        lead.ChangeStatus(LeadStatus.Lost, " ", At).Error.ShouldBe(LeadErrors.LostReasonRequired);
        lead.ChangeStatus(LeadStatus.Won, null, At).Error.ShouldBe(LeadErrors.WonRequiresEnrollment);
        lead.ChangeStatus(LeadStatus.Lost, "Đã học chỗ khác", At).IsSuccess.ShouldBeTrue();
        lead.LostReason.ShouldBe("Đã học chỗ khác");
    }

    [Fact]
    public void CRM_04_E3_mo_lai_lead_mat_xoa_ly_do_ghi_dong_he_thong()
    {
        var lead = New(LeadStatus.Lost, "Học phí cao");
        lead.ChangeStatus(LeadStatus.Consulting, null, At).IsSuccess.ShouldBeTrue();
        lead.Status.ShouldBe(LeadStatus.Consulting);
        lead.LostReason.ShouldBeNull();
        var note = lead.Consultations.ShouldHaveSingleItem();
        (note.IsSystem, note.Content, note.StaffId).ShouldBe((true, "Mở lại lead", (Guid?)null));
    }

    [Fact]
    public void Lead_da_chot_khong_doi_giai_doan()
    {
        var lead = New();
        lead.LinkStudent(Kid, "An", Relationship.Child, true, true, true, At);
        lead.MarkWon(Kid, "Ghi danh An vào lớp N5", At);
        lead.ChangeStatus(LeadStatus.Lost, "x", At).Error.ShouldBe(LeadErrors.WonLocked);
        lead.ChangeStatus(LeadStatus.Won, null, At).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CRM_08_lien_ket_hoc_vien_khong_doi_giai_doan_ghi_dong_he_thong()
    {
        var lead = New();
        lead.ClearDomainEvents();
        lead.LinkStudent(Kid, "Gia Bảo", Relationship.Child, true, true, false, At).IsSuccess.ShouldBeTrue();

        lead.Status.ShouldBe(LeadStatus.New);
        var link = lead.StudentLinks.ShouldHaveSingleItem();
        (link.StudentId, link.IsPrimary, link.Billing, link.Notify).ShouldBe((Kid, true, true, false));
        lead.Consultations.ShouldHaveSingleItem().Content.ShouldBe("Liên kết học viên Gia Bảo (con)");
        lead.DomainEvents.ShouldHaveSingleItem().ShouldBe(new LeadStudentLinked(lead.Id, Kid));
        lead.LinkStudent(Kid, "Gia Bảo", Relationship.Child, false, false, false, At).Error.ShouldBe(LeadErrors.AlreadyLinked);
    }

    [Fact]
    public void CRM_07_E1_moi_lead_chi_1_ho_so_ban_than()
    {
        var lead = New();
        lead.LinkStudent(Kid, "Bình", Relationship.Self, true, true, true, At);
        lead.LinkStudent(Kid2, "Bình 2", Relationship.Self, true, true, true, At).Error.ShouldBe(LeadErrors.SelfAlreadyLinked);
        lead.LinkStudent(Kid2, "Hoa", Relationship.Spouse, false, false, true, At).IsSuccess.ShouldBeTrue();
        lead.SetLinkFlags(Kid2, Relationship.Self, false, false, true).Error.ShouldBe(LeadErrors.SelfAlreadyLinked);
    }

    [Fact]
    public void CRM_08_doi_co_va_E2_tat_lien_he_chinh()
    {
        var lead = New();
        lead.LinkStudent(Kid, "An", Relationship.Child, true, false, false, At);
        lead.SetLinkFlags(Kid, Relationship.Child, true, true, true).IsSuccess.ShouldBeTrue();
        lead.StudentLinks[0].Billing.ShouldBeTrue();
        lead.SetLinkFlags(Kid2, Relationship.Child, true, true, true).Error.ShouldBe(LeadErrors.NotLinked);

        lead.ClearPrimary(Kid).ShouldBeTrue();
        lead.StudentLinks[0].IsPrimary.ShouldBeFalse();
        lead.StudentLinks[0].Billing.ShouldBeTrue();
        lead.ClearPrimary(Kid).ShouldBeFalse();
    }

    [Fact]
    public void CRM_08_go_lien_ket_phat_su_kien()
    {
        var lead = New();
        lead.LinkStudent(Kid, "An", Relationship.Child, true, true, true, At);
        lead.ClearDomainEvents();
        lead.UnlinkStudent(Kid, "An", At).IsSuccess.ShouldBeTrue();
        lead.StudentLinks.ShouldBeEmpty();
        lead.DomainEvents.ShouldHaveSingleItem().ShouldBe(new LeadStudentUnlinked(lead.Id, Kid));
        lead.Consultations.Last().Content.ShouldBe("Gỡ liên kết học viên An");
        lead.UnlinkStudent(Kid, "An", At).Error.ShouldBe(LeadErrors.NotLinked);
    }

    [Fact]
    public void CRM_07_chot_lead_can_hoc_vien_lien_ket_chot_lan_2_chi_ghi_chu()
    {
        var lead = New(LeadStatus.Lost, "Bận");
        lead.MarkWon(Kid, "x", At).Error.ShouldBe(LeadErrors.NotLinked);

        lead.LinkStudent(Kid, "An", Relationship.Child, true, true, true, At);
        lead.LinkStudent(Kid2, "Bảo", Relationship.Child, true, true, true, At);
        lead.ClearDomainEvents();
        lead.MarkWon(Kid, "Ghi danh An vào lớp N5K01", At).IsSuccess.ShouldBeTrue();
        lead.Status.ShouldBe(LeadStatus.Won);
        lead.LostReason.ShouldBeNull();
        lead.DomainEvents.ShouldHaveSingleItem().ShouldBe(new LeadWon(lead.Id, Kid));

        lead.MarkWon(Kid2, "Ghi danh Bảo vào lớp N5K01", At).IsSuccess.ShouldBeTrue();
        lead.DomainEvents.Count.ShouldBe(1);
        lead.Consultations.Count(c => c.Result == ConsultResult.Enrolled).ShouldBe(2);
    }

    [Fact]
    public void CRM_06_an_lead_E1_da_an()
    {
        var lead = New();
        lead.Hide(At).IsSuccess.ShouldBeTrue();
        lead.Hide(At).Error.ShouldBe(LeadErrors.AlreadyHidden);
        lead.Update(Info(name: "Tên mới")).IsSuccess.ShouldBeTrue();
        lead.Hidden.ShouldBeTrue();
        lead.Unhide();
        (lead.Hidden, lead.HiddenAt).ShouldBe((false, (DateTimeOffset?)null));
    }

    [Fact]
    public void CRM_01_E2_E3_qua_sla_va_qua_hen()
    {
        var created = At.AddMinutes(-15);
        var lead = New();
        lead.IsSlaOverdue(created, 15, At).ShouldBeTrue();
        lead.IsSlaOverdue(created, 16, At).ShouldBeFalse();
        lead.Hide(At);
        lead.IsSlaOverdue(created, 15, At).ShouldBeFalse();

        var won = New();
        won.ScheduleFollowUp(At.AddMinutes(-1));
        won.IsFollowUpOverdue(At).ShouldBeTrue();
        won.ChangeStatus(LeadStatus.Lost, "x", At);
        won.IsFollowUpOverdue(At).ShouldBeFalse();
    }

    [Fact]
    public void CRM_11_E1_canh_bao_va_tu_chuyen_moi_lead_1_lan()
    {
        var lead = New();
        lead.AssignTo(Sale);
        lead.MarkSlaEscalated(At).ShouldBeTrue();
        lead.MarkSlaEscalated(At.AddMinutes(1)).ShouldBeFalse();
        lead.SlaEscalatedAt.ShouldBe(At);

        lead.ReassignForSla(Sale2, At).IsSuccess.ShouldBeTrue();
        lead.AssigneeId.ShouldBe(Sale2);
        lead.ReassignForSla(Sale, At).Error.ShouldBe(LeadErrors.SlaAlreadyReassigned);
    }

    [Fact]
    public void CRM_13_ghi_ket_qua_test_cap_nhat_khoa_quan_tam()
    {
        var lead = New();
        lead.RecordPlacementResult("Test đầu vào", 14, 20, 70m, "N4", "N4 tối 2-4-6", At);
        lead.Interest.ShouldBe("N4 tối 2-4-6");
        var note = lead.Consultations.ShouldHaveSingleItem();
        note.IsSystem.ShouldBeTrue();
        note.Content.ShouldBe("Kết quả test \"Test đầu vào\": 14/20 câu (70%) — gợi ý N4 · N4 tối 2-4-6");
    }

    [Fact]
    public void HV_12_them_phu_huynh_vao_thang_da_chot_kem_lien_ket()
    {
        var lead = Lead.CreateGuardian("LD0050", Info("Lê Thị Mai", "0987000111"), [(Kid, "Bé An"), (Kid2, "Bé Bình"), (Kid, "Bé An")],
            Relationship.Child, isPrimary: true, At).Value;
        lead.Status.ShouldBe(LeadStatus.Won);
        lead.StudentLinks.Count.ShouldBe(2);
        lead.StudentLinks.ShouldAllBe(k => k.Relationship == Relationship.Child && k.IsPrimary && k.Billing && k.Notify);
        lead.Consultations.ShouldHaveSingleItem().IsSystem.ShouldBeTrue();
        lead.DomainEvents.OfType<LeadStudentLinked>().Count().ShouldBe(2);
        lead.DomainEvents.OfType<LeadCreated>().ShouldHaveSingleItem();
    }

    [Fact]
    public void HV_12_E2_can_hoc_vien_va_quan_he_khac_ban_than()
    {
        Lead.CreateGuardian("x", Info(), [], Relationship.Child, false, At).Error.ShouldBe(LeadErrors.StudentsRequired);
        Lead.CreateGuardian("x", Info(), [(Kid, "Bé")], Relationship.Self, false, At).Error.ShouldBe(LeadErrors.GuardianCannotBeSelf);
        Lead.CreateGuardian("x", Info(phone: "123"), [(Kid, "Bé")], Relationship.Other, false, At).Error.ShouldBe(LeadErrors.InvalidPhone);
        var lead = Lead.CreateGuardian("x", Info(), [(Kid, "Bé")], Relationship.Sibling, false, At).Value;
        lead.StudentLinks.ShouldHaveSingleItem().Billing.ShouldBeFalse();
    }
}
