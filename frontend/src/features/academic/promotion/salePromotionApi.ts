/**
 * Tư vấn lên lớp phía Sale (CRM-19) — dữ liệu lên lớp thuộc Academic: /api/v1/academic/promotions/sale, …/notes|unreachable|decline|confirm.
 * Server trả kèm NGƯỜI QUYẾT ĐỊNH (phụ huynh liên hệ chính; không có = chính HV) để gom các con của cùng 1 phụ huynh.
 * Chốt = ghi danh thật trên server; ⏳ hóa đơn còn giả (Tài chính — giai đoạn 19).
 */
import { api } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { toPromotion, type ServerPromotion } from '@/features/portal/portalApi';
import { loadEnrollments } from '@/features/student/studentApi';
import { refreshInvoicesSoon } from '@/features/finance/financeApi';
import type { ConsultChannel } from '@/features/crm/types';
import { usePromotionStore } from './promotionStore';
import type { PromotionCandidate } from './types';

/** Người quyết định server tính theo Id học viên (null = học viên tự quyết) */
export const serverDeciders = new Map<string, { leadId: string; name: string; phone: string } | null>();

function upsert(list: PromotionCandidate[]) {
  const ids = new Set(list.map((c) => c.id));
  usePromotionStore.setState((s) => ({ candidates: [...list, ...s.candidates.filter((c) => !ids.has(c.id))] }));
}

export async function loadSalePromotions() {
  const list = await api<{ promotion: ServerPromotion; decider?: { leadId: string; fullName: string; phone: string } | null }[]>('/academic/promotions/sale');
  list.forEach((x) =>
    serverDeciders.set(fromGuid(x.promotion.studentId), x.decider ? { leadId: fromGuid(x.decider.leadId), name: x.decider.fullName, phone: x.decider.phone } : null),
  );
  upsert(list.map((x) => toPromotion(x.promotion)));
}

const one = async (path: string, body?: unknown) => {
  const p = toPromotion(await api<ServerPromotion>(path, { body: body ?? {} }));
  upsert([p]);
  return p;
};

/** Ghi chú tư vấn (chờ → đang tư vấn); "chưa liên hệ được" gọi riêng */
export const addSaleNote = (id: string, text: string, channel?: ConsultChannel) => one(`/academic/promotions/${toGuid(id)}/notes`, { text, channel: channel ?? null });

export const markUnreachable = (id: string) => one(`/academic/promotions/${toGuid(id)}/unreachable`);

export const declinePromotion = (id: string, reason: string) => one(`/academic/promotions/${toGuid(id)}/decline`, { text: reason, channel: null });

/** Chốt: ghi danh HV vào lớp đề xuất (server kiểm còn chỗ / chưa học lớp) */
export async function confirmPromotion(id: string, idempotencyKey: string) {
  const p = toPromotion(
    await api<ServerPromotion>(`/academic/promotions/${toGuid(id)}/confirm`, {
      headers: { 'Idempotency-Key': idempotencyKey },
      body: { amount: null, discount: 0, dueDate: null },
    }),
  );
  upsert([p]);
  if (p.toClassId) {
    await loadEnrollments(p.toClassId).catch(() => undefined);
    // Finance lập hóa đơn lớp mới ở nền (TC-04)
    refreshInvoicesSoon();
  }
  return p;
}
