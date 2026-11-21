import { USE_API } from '@/lib/api/config';
import { useEffect } from 'react';
import dayjs from 'dayjs';
import { useHrStore } from '@/features/hr/hrStore';
import { useNotificationStore } from '@/features/notifications/notificationStore';
import { useWorkStore } from '@/features/work/workStore';
import { getSettings } from '@/features/system/settings';
import { saleName, salesOf } from '../constants';
import { isSlaOverdue, useCrmStore } from '../crmStore';
import type { Lead, LeadRule } from '../types';
import { useAdmissionStore } from './admissionStore';
import { isOnShift, matchRule, openLoad } from './routing';

/**
 * THEO DÕI SLA — chạy định kỳ (backend sẽ chạy đúng logic này bằng job mỗi phút).
 *  1. Lead mới quá SLA chưa ai liên hệ → báo cho người trong "Quá hạn báo cho" của quy tắc (mỗi lead 1 lần).
 *  2. Quy tắc bật "tự chuyển" + quá 2×SLA → chuyển sang Sale khác trong nhóm (ưu tiên người trong ca, ít lead mở nhất),
 *     ghi nhật ký chia lead (mỗi lead tự chuyển 1 lần — sau đó quản lý xử lý tay).
 * Gom thông báo: nhiều hơn 3 lead cùng lúc → 1 thông báo tổng hợp để không ngập chuông.
 */

const BATCH_LIMIT = 3;

/** Sale thay thế: cùng nhóm quy tắc, còn làm việc, khác người đang giữ; ưu tiên trong ca rồi ít lead mở nhất */
export function pickReplacement(rule: LeadRule, lead: Lead, leads: Lead[], at = dayjs()): string | undefined {
  const employees = useHrStore.getState().employees;
  const shifts = useWorkStore.getState().shifts;
  const active = salesOf(employees);
  const team = rule.saleIds.filter((id) => id !== lead.assigneeId && active.some((e) => e.id === id));
  if (!team.length) return undefined;
  const onShift = team.filter((id) => isOnShift(id, shifts, at, employees));
  const pool = onShift.length ? onShift : team;
  const load = openLoad(leads);
  return [...pool].sort((a, b) => (load.get(a) ?? 0) - (load.get(b) ?? 0))[0];
}

const lateLabel = (min: number) => (min >= 60 ? `${Math.floor(min / 60)} giờ ${min % 60 ? `${min % 60} phút` : ''}`.trim() : `${min} phút`);

export function runSlaCheck(at = dayjs()): { escalated: number; reassigned: number } {
  const crm = useCrmStore.getState();
  const adm = useAdmissionStore.getState();
  const notify = useNotificationStore.getState().add;
  const stamp = at.toISOString();

  const escalated: { lead: Lead; late: number; to: string[] }[] = [];
  const reassigned: { lead: Lead; from?: string; to: string }[] = [];

  for (const lead of crm.leads) {
    if (lead.hidden) continue;
    const rule = matchRule(adm.rules, lead);
    const sla = rule?.slaMinutes ?? getSettings().crm.defaultSlaMinutes;
    if (!isSlaOverdue(lead, sla)) continue;
    const late = at.diff(dayjs(lead.createdAt), 'minute') - sla;

    if (!lead.slaEscalatedAt) {
      crm.updateLead(lead.id, { slaEscalatedAt: stamp });
      escalated.push({ lead, late, to: rule?.escalateTo.length ? rule.escalateTo : ['Quản lý chi nhánh'] });
    }

    // Quá 2×SLA (tức trễ thêm ≥ 1 SLA) → tự chuyển
    if (rule?.reassignOnBreach && !lead.slaReassignedAt && late >= sla) {
      const next = pickReplacement(rule, lead, useCrmStore.getState().leads, at);
      if (!next) continue;
      crm.updateLead(lead.id, { assigneeId: next, slaReassignedAt: stamp });
      adm.addLog({
        leadId: lead.id,
        saleId: next,
        ruleId: rule.id,
        reason: `Tự chuyển do quá 2×SLA (${sla * 2} phút) — trước: ${saleName(lead.assigneeId) ?? 'chưa giao'}`,
      });
      reassigned.push({ lead, from: lead.assigneeId, to: next });
    }
  }

  // ---- Thông báo ----
  if (escalated.length > BATCH_LIMIT) {
    notify('staff', {
      title: `${escalated.length} lead quá hạn liên hệ`,
      description: `Chưa ai gọi trong thời hạn SLA. Báo cho: ${[...new Set(escalated.flatMap((x) => x.to))].join(', ')}.`,
      tone: 'danger',
      link: '/admin/crm/lead-rules',
    });
  } else {
    escalated.forEach(({ lead, late, to }) =>
      notify('staff', {
        title: `Lead quá SLA: ${lead.fullName}`,
        description: `${saleName(lead.assigneeId) ?? 'Chưa giao Sale'} · trễ ${lateLabel(late)}. Báo cho: ${to.join(', ')}.`,
        tone: 'danger',
        link: `/admin/crm/leads?open=${lead.id}`,
      }),
    );
  }

  if (reassigned.length > BATCH_LIMIT) {
    notify('staff', {
      title: `Đã tự chuyển ${reassigned.length} lead quá hạn`,
      description: 'Quá gấp đôi SLA mà chưa liên hệ — đã giao cho Sale khác trong nhóm. Xem nhật ký chia lead.',
      tone: 'warning',
      link: '/admin/crm/lead-rules',
    });
  } else {
    reassigned.forEach(({ lead, from, to }) =>
      notify('staff', {
        title: `Đã chuyển lead ${lead.fullName}`,
        description: `${saleName(from) ?? 'Chưa giao'} → ${saleName(to)} (quá gấp đôi SLA chưa liên hệ).`,
        tone: 'warning',
        link: `/admin/crm/leads?open=${lead.id}`,
      }),
    );
  }

  return { escalated: escalated.length, reassigned: reassigned.length };
}

/** Chạy kiểm tra SLA khi vào cổng nhân viên và mỗi phút sau đó */
export function useSlaWatcher(intervalMs = 60_000) {
  useEffect(() => {
    if (USE_API) return; // Chế độ API: job server làm (CrmMinuteJob)
    runSlaCheck();
    const t = window.setInterval(() => runSlaCheck(), intervalMs);
    return () => window.clearInterval(t);
  }, [intervalMs]);
}
