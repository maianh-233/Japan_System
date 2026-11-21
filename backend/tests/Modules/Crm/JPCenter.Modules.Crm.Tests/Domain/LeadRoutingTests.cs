using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Tests.Domain;

public sealed class LeadRoutingTests
{
    private static readonly BranchId Cg = new(Guid.NewGuid()), Q1 = new(Guid.NewGuid());
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();
    private static readonly Dictionary<Guid, string> Names = new() { [A] = "An", [B] = "Bình", [C] = "Chi" };
    private static readonly DateTimeOffset At = new(2026, 11, 21, 2, 0, 0, TimeSpan.Zero);

    private static LeadRuleSettings Settings(string name = "Facebook CG", RouteMethod method = RouteMethod.RoundRobin, Guid[]? sales = null,
        LeadSource[]? sources = null, BranchId[]? branches = null, string[]? interests = null, bool onShift = false, int sla = 15,
        bool reassign = false) =>
        new(name, sources ?? [], branches ?? [], interests ?? [], method, sales ?? [A, B, C], onShift, sla, [], reassign);

    private static LeadRule Rule(LeadRuleSettings? s = null, int priority = 1) => LeadRule.Create(s ?? Settings(), priority).Value;

    private static RoutingContext Ctx(Guid[]? working = null, Guid[]? onShift = null, Dictionary<Guid, int>? load = null) =>
        new(id => (working ?? [A, B, C]).Contains(id), id => (onShift ?? []).Contains(id), id => load?.GetValueOrDefault(id) ?? 0,
            id => Names[id]);

    [Fact]
    public void CRM_09_E1_E2_kiem_tra_quy_tac()
    {
        LeadRule.Create(Settings(sales: []), 1).Error.ShouldBe(LeadRuleErrors.NoSale);
        LeadRule.Create(Settings(sla: 0), 1).Error.ShouldBe(LeadRuleErrors.InvalidSla);
        LeadRule.Create(Settings(sla: 7 * 24 * 60 + 1), 1).Error.ShouldBe(LeadRuleErrors.InvalidSla);
        LeadRule.Create(Settings(name: " "), 1).Error.ShouldBe(LeadRuleErrors.NameRequired);
        var rule = Rule(Settings(sales: [A, A, B], interests: [" N5 ", "", "N5"]));
        rule.SaleIds.ShouldBe([A, B]);
        rule.Interests.ShouldBe(["N5"]);
        rule.Cursor.ShouldBe(-1);
    }

    [Fact]
    public void Doi_nhom_sale_dat_lai_con_tro()
    {
        var rule = Rule();
        rule.MoveCursor(2);
        rule.Update(Settings(name: "Đổi tên")).IsSuccess.ShouldBeTrue();
        rule.Cursor.ShouldBe(2);
        rule.Update(Settings(sales: [C, A])).IsSuccess.ShouldBeTrue();
        rule.Cursor.ShouldBe(-1);
    }

    [Fact]
    public void CRM_10_quy_tac_dau_tien_khop_theo_uu_tien_bo_qua_quy_tac_tat()
    {
        var fb = Rule(Settings("Facebook", sources: [LeadSource.Facebook], sales: [B]), priority: 2);
        var q1 = Rule(Settings("Q1", branches: [Q1], sales: [C]), priority: 1);
        var off = Rule(Settings("Tắt", sales: [A]), priority: 0);
        off.Disable();
        var rules = new[] { fb, q1, off };

        LeadRouter.MatchRule(rules, LeadSource.Facebook, Q1, null).ShouldBe(q1);
        LeadRouter.MatchRule(rules, LeadSource.Facebook, Cg, null).ShouldBe(fb);
        LeadRouter.MatchRule(rules, LeadSource.Zalo, Cg, null).ShouldBeNull();
    }

    [Fact]
    public void Dieu_kien_khoa_quan_tam_can_co_gia_tri()
    {
        var rule = Rule(Settings(interests: ["Du học"]));
        rule.Matches(LeadSource.Zalo, Cg, null).ShouldBeFalse();
        rule.Matches(LeadSource.Zalo, Cg, " Du học ").ShouldBeTrue();
    }

    [Fact]
    public void CRM_10_E1_khong_quy_tac_khop()
    {
        var d = LeadRouter.Route([], LeadSource.Zalo, Cg, null, Ctx());
        (d.SaleId, d.Rule, d.Reason).ShouldBe((null, null, "Không quy tắc nào khớp — chờ quản lý giao tay"));
    }

    [Fact]
    public void CRM_10_E2_khong_con_sale_dang_lam()
    {
        var d = LeadRouter.Route([Rule()], LeadSource.Zalo, Cg, null, Ctx(working: []));
        d.SaleId.ShouldBeNull();
        d.Reason.ShouldBe("Quy tắc \"Facebook CG\" không còn Sale nào đang làm việc");
    }

    [Fact]
    public void Round_robin_di_tiep_tu_con_tro_bo_qua_nguoi_nghi()
    {
        var rule = Rule();
        var first = LeadRouter.Route([rule], LeadSource.Zalo, Cg, null, Ctx());
        (first.SaleId, first.Cursor).ShouldBe((A, 0));
        rule.MoveCursor(first.Cursor!.Value);

        var second = LeadRouter.Route([rule], LeadSource.Zalo, Cg, null, Ctx(working: [A, C]));
        // B nghỉ việc → nhóm còn [A, C], con trỏ 0 → người kế là C
        (second.SaleId, second.Cursor).ShouldBe((C, 1));
        second.Reason.ShouldBe("Quy tắc \"Facebook CG\" → Chi");
    }

    [Fact]
    public void Round_robin_chi_trong_ca()
    {
        var rule = Rule(Settings(onShift: true));
        var d = LeadRouter.Route([rule], LeadSource.Zalo, Cg, null, Ctx(onShift: [C]));
        (d.SaleId, d.Cursor).ShouldBe((C, 2));
        d.Reason.ShouldBe("Quy tắc \"Facebook CG\" → Chi (1/3 Sale đang trong ca)");

        var nobody = LeadRouter.Route([rule], LeadSource.Zalo, Cg, null, Ctx(onShift: []));
        nobody.SaleId.ShouldBe(A);
        nobody.Reason.ShouldEndWith("(không ai trong ca → chia cho cả nhóm)");
    }

    [Fact]
    public void Least_load_chon_it_lead_mo_nhat_bang_nhau_lay_nguoi_dung_truoc()
    {
        var rule = Rule(Settings(method: RouteMethod.LeastLoad));
        var d = LeadRouter.Route([rule], LeadSource.Zalo, Cg, null, Ctx(load: new() { [A] = 3, [B] = 1, [C] = 1 }));
        (d.SaleId, d.Cursor).ShouldBe((B, null));
        d.Reason.ShouldBe("Quy tắc \"Facebook CG\" → Bình (đang giữ 1 lead mở — ít nhất)");
    }

    [Fact]
    public void Fixed_luon_nguoi_dau_con_lam()
    {
        var rule = Rule(Settings(method: RouteMethod.Fixed));
        LeadRouter.Route([rule], LeadSource.Zalo, Cg, null, Ctx()).SaleId.ShouldBe(A);
        LeadRouter.Route([rule], LeadSource.Zalo, Cg, null, Ctx(working: [B, C])).SaleId.ShouldBe(B);
    }

    [Fact]
    public void CRM_11_sale_thay_the_khac_nguoi_giu_uu_tien_trong_ca_roi_it_lead()
    {
        var rule = Rule();
        LeadRouter.PickReplacement(rule, A, Ctx(load: new() { [B] = 5, [C] = 2 })).ShouldBe(C);
        LeadRouter.PickReplacement(rule, A, Ctx(onShift: [B], load: new() { [B] = 5, [C] = 2 })).ShouldBe(B);
        LeadRouter.PickReplacement(Rule(Settings(sales: [A])), A, Ctx()).ShouldBeNull();
    }

    [Fact]
    public void CRM_11_kiem_sla_canh_bao_roi_tu_chuyen_khi_qua_2_lan()
    {
        var rule = Rule(Settings(sla: 15, reassign: true));
        var lead = Lead.Create("LD0001", new LeadInfo("Hoa", "0912345678", null, LeadSource.Zalo, Cg, null, null, null, null)).Value;
        var created = At.AddMinutes(-20);

        var early = SlaCheck.Of(lead, created, rule, 30, At);
        (early.Overdue, early.SlaMinutes, early.LateMinutes, early.Escalate, early.Reassign).ShouldBe((true, 15, 5, true, false));

        lead.MarkSlaEscalated(At);
        var late = SlaCheck.Of(lead, At.AddMinutes(-30), rule, 30, At);
        (late.Escalate, late.Reassign).ShouldBe((false, true));

        SlaCheck.Of(lead, created, null, 30, At).Overdue.ShouldBeFalse();
        lead.AddConsultation(ConsultChannel.Call, ConsultResult.Interested, "Đã gọi", A, "An", At, null);
        SlaCheck.Of(lead, At.AddHours(-5), rule, 30, At).Overdue.ShouldBeFalse();
    }
}
