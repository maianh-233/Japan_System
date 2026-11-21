/**
 * Phễu chuyển đổi từ server (/api/v1/crm/funnel — CRM-18): hành trình từng lead theo ngày tạo, phạm vi own = lead mình phụ trách.
 * Frontend gom theo Sale / nguồn / chiến dịch bằng `summarize` như chế độ mock.
 */
import { api } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useCrmStore } from './crmStore';
import { STAGES, type FunnelStage, type LeadJourney } from './funnel';
import type { Lead, LeadSource, LeadStatus } from './types';

interface ServerJourney {
  leadId: string;
  code: string;
  fullName: string;
  source: LeadSource;
  branchId: string;
  assigneeId?: string | null;
  campaignId?: string | null;
  status: LeadStatus;
  createdAt: string;
  calledAt?: string | null;
  testAt?: string | null;
  wonAt?: string | null;
  stage: FunnelStage;
  lost: boolean;
  stuckDays?: number | null;
}

/** `from` = ngày bắt đầu (YYYY-MM-DD), bỏ trống = tất cả */
export async function loadFunnel(from: string | undefined, branchId: string | undefined): Promise<LeadJourney[]> {
  const q = new URLSearchParams();
  if (from) q.set('from', from);
  if (branchId) q.set('branchId', toGuid(branchId));
  const list = await api<ServerJourney[]>(`/crm/funnel?${q}`);
  const leads = useCrmStore.getState().leads;
  return list.map((j) => {
    const id = fromGuid(j.leadId);
    const lead: Lead = leads.find((l) => l.id === id) ?? {
      id,
      code: j.code,
      fullName: j.fullName,
      phone: '',
      source: j.source,
      status: j.status,
      branchId: fromGuid(j.branchId),
      assigneeId: j.assigneeId ? fromGuid(j.assigneeId) : undefined,
      campaignId: j.campaignId ? fromGuid(j.campaignId) : undefined,
      createdAt: j.createdAt,
      createdBy: '',
      hidden: false,
    };
    const level = STAGES.indexOf(j.stage);
    return {
      lead: { ...lead, assigneeId: j.assigneeId ? fromGuid(j.assigneeId) : undefined, campaignId: j.campaignId ? fromGuid(j.campaignId) : undefined, status: j.status },
      at: { lead: j.createdAt, called: j.calledAt ?? undefined, test: j.testAt ?? undefined, won: j.wonAt ?? undefined },
      reached: { lead: true, called: level >= 1, test: level >= 2, won: level >= 3 },
      stage: j.stage,
      lost: j.lost,
      stuckDays: j.stuckDays ?? undefined,
    };
  });
}
