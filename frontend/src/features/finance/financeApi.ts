/**
 * Tài chính (user story 08, giai đoạn 19) — backend Finance: /api/v1/finance.
 * Đổ dữ liệu server vào store cũ (useStudentStore.invoices, useFinanceStore.prices / promotions / plans / txns / reminders)
 * nên các trang đọc store như trước. Hóa đơn chỉ sinh từ ghi danh — server lập NỀN (sự kiện) nên sau ghi danh / chuyển lớp / cho nghỉ
 * tải lại hóa đơn sau chốc lát. Hoàn tiền / hủy hóa đơn đi qua hộp duyệt chung (System) — người thứ 2 duyệt mới ghi.
 * ⏳ Lương GV / NV, hoa hồng (giai đoạn 22) vẫn là dữ liệu giả.
 */
import { useStudentStore } from '@/features/student/studentStore';
import type { Invoice, PaymentMethod, TuitionStatus } from '@/features/student/types';
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, fromGuids, toGuid, toGuids } from '@/lib/api/ids';
import { useFinanceStore } from './financeStore';
import type { BankTxn, BranchPrice, InstallmentPlan, Promotion, Reminder, ReminderChannel, TxnStatus } from './types';

// ---------- Kiểu dữ liệu server ----------

interface ServerPayment {
  id: string;
  amount: number;
  method: PaymentMethod;
  paidAt: string;
  collector: string;
  note?: string | null;
}

interface ServerRefund {
  id: string;
  amount: number;
  method: PaymentMethod;
  refundedAt: string;
  reason: string;
  by: string;
  approvedBy?: string | null;
}

interface ServerReminder {
  id: string;
  invoiceId: string;
  channel: ReminderChannel;
  at: string;
  by?: string | null;
  note?: string | null;
  milestone?: 'before_due' | 'on_due' | 'overdue' | null;
}

interface ServerInvoice {
  id: string;
  code: string;
  enrollmentId: string;
  studentId: string;
  branchId: string;
  amount: number;
  discount: number;
  dueDate: string;
  issuedAt: string;
  transferContent: string;
  status: TuitionStatus | 'voided';
  payments: ServerPayment[];
  refunds: ServerRefund[];
  cancelled: boolean;
  voided?: { reason: string; requestedBy: string; approvedBy: string; at: string } | null;
  reminders: ServerReminder[];
}

interface ServerActionResult {
  invoice: ServerInvoice;
  approval?: { id: string; code: string } | null;
}

interface ServerDiscount {
  id: string;
  code: string;
  name: string;
  kind: Promotion['kind'];
  value: number;
  courseIds: string[];
  branchIds: string[];
  from: string;
  to: string;
  limit?: number | null;
  used: number;
  active: boolean;
  note?: string | null;
}

interface ServerPlan {
  id: string;
  name: string;
  parts: { percent: number; offsetDays: number }[];
  minFee: number;
  surcharge: number;
  active: boolean;
}

interface ServerTxn {
  id: string;
  ref: string;
  at: string;
  amount: number;
  content: string;
  payer: string;
  account: string;
  branchId: string;
  status: TxnStatus;
  invoiceId?: string | null;
  invoiceIds: string[];
  matchedBy?: string | null;
  matchedAt?: string | null;
  note?: string | null;
}

interface ServerPortalInvoice {
  id: string;
  code: string;
  enrollmentId: string;
  branchId: string;
  amount: number;
  discount: number;
  dueDate: string;
  issuedAt: string;
  payments: { amount: number; method: PaymentMethod; at: string }[];
  refunds: { amount: number; method: PaymentMethod; at: string }[];
  status: TuitionStatus | 'voided';
  qr?: { bankName: string; account: string; holder: string; amount: number; content: string } | null;
}

// ---------- Đổi sang kiểu frontend ----------

const AUTO_REMINDER = 'Hệ thống (tự nhắc)';

const toInvoice = (i: ServerInvoice): Invoice => ({
  id: fromGuid(i.id),
  code: i.code,
  enrollmentId: fromGuid(i.enrollmentId),
  amount: i.amount,
  discount: i.discount,
  dueDate: i.dueDate,
  issuedAt: i.issuedAt,
  payments: i.payments.map((p) => ({ id: fromGuid(p.id), amount: p.amount, method: p.method, paidAt: p.paidAt, collector: p.collector })),
  refunds: i.refunds.map((r) => ({
    id: fromGuid(r.id),
    amount: r.amount,
    method: r.method,
    refundedAt: r.refundedAt,
    reason: r.reason,
    by: r.by,
    approvedBy: r.approvedBy ?? undefined,
  })),
  // Hủy hóa đơn cũng hết công nợ (như dữ liệu giả cũ)
  cancelled: i.cancelled || !!i.voided || undefined,
  voided: i.voided ?? undefined,
  transferContent: i.transferContent,
});

const toReminder = (r: ServerReminder): Reminder => ({
  id: r.id,
  invoiceId: fromGuid(r.invoiceId),
  channel: r.channel,
  at: r.at,
  by: r.by ?? AUTO_REMINDER,
  note: r.note ?? undefined,
});

const toPromotion = (d: ServerDiscount): Promotion => ({
  id: d.id,
  code: d.code,
  name: d.name,
  kind: d.kind,
  value: d.value,
  courseIds: fromGuids(d.courseIds),
  branchIds: fromGuids(d.branchIds),
  from: d.from,
  to: d.to,
  limit: d.limit ?? undefined,
  used: d.used,
  active: d.active,
  note: d.note ?? undefined,
});

const toPlan = (p: ServerPlan): InstallmentPlan => ({ id: p.id, name: p.name, parts: p.parts, minFee: p.minFee, surcharge: p.surcharge, active: p.active });

const toTxn = (t: ServerTxn): BankTxn => ({
  id: t.id,
  ref: t.ref,
  at: t.at,
  amount: t.amount,
  content: t.content,
  payer: t.payer,
  account: t.account,
  branchId: fromGuid(t.branchId),
  status: t.status,
  invoiceId: t.invoiceId ? fromGuid(t.invoiceId) : undefined,
  matchedBy: t.matchedBy ?? (t.status === 'auto' ? 'Hệ thống' : undefined),
  matchedAt: t.matchedAt ?? undefined,
  note: t.note ?? undefined,
});

/** Bỏ qua 403 (không có quyền trang đó → rỗng) */
async function optional<T>(p: Promise<T>): Promise<T | undefined> {
  try {
    return await p;
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) return undefined;
    throw e;
  }
}

function upsertInvoice(i: ServerInvoice) {
  const inv = toInvoice(i);
  useStudentStore.setState((s) => ({ invoices: [...s.invoices.filter((x) => x.id !== inv.id), inv] }));
  useFinanceStore.setState((s) => ({ reminders: [...s.reminders.filter((r) => r.invoiceId !== inv.id), ...i.reminders.map(toReminder)] }));
  return inv;
}

// ---------- Tải dữ liệu ----------

/** Hóa đơn trong phạm vi (kể cả đã hủy — trang Hóa đơn lọc "Đã hủy") + lịch sử nhắc. Không có quyền → rỗng */
export async function loadInvoices() {
  const [open, voided] = await Promise.all([
    optional(api<ServerInvoice[]>('/finance/invoices')),
    optional(api<ServerInvoice[]>('/finance/invoices?status=voided')),
  ]);
  const all = [...(open ?? []), ...(voided ?? [])];
  useStudentStore.setState({ invoices: all.map(toInvoice) });
  useFinanceStore.setState({ reminders: all.flatMap((i) => i.reminders.map(toReminder)) });
}

/** Bảng giá chi nhánh, ưu đãi, chính sách đóng đợt */
export async function loadPricing() {
  const [prices, discounts, plans] = await Promise.all([
    optional(api<{ courseId: string; branchId: string; fee: number }[]>('/finance/branch-prices')),
    optional(api<ServerDiscount[]>('/finance/discounts')),
    optional(api<ServerPlan[]>('/finance/installment-plans')),
  ]);
  useFinanceStore.setState({
    prices: (prices ?? []).map((p): BranchPrice => ({ courseId: fromGuid(p.courseId), branchId: fromGuid(p.branchId), fee: p.fee })),
    promotions: (discounts ?? []).map(toPromotion),
    plans: (plans ?? []).map(toPlan),
  });
}

/** Giao dịch ngân hàng (Đối soát VietQR) */
export async function loadTransactions() {
  const list = await optional(api<ServerTxn[]>('/finance/bank-transactions'));
  useFinanceStore.setState({ txns: (list ?? []).map(toTxn) });
}

/** Cổng HV / PH: học phí của mình / các con (không có người thu, ghi chú nội bộ) — VietQR server trả sẵn */
export async function loadPortalInvoices() {
  const list = await api<ServerPortalInvoice[]>('/finance/portal/invoices');
  useStudentStore.setState({
    invoices: list.map(
      (i): Invoice => ({
        id: fromGuid(i.id),
        code: i.code,
        enrollmentId: fromGuid(i.enrollmentId),
        amount: i.amount,
        discount: i.discount,
        dueDate: i.dueDate,
        issuedAt: i.issuedAt,
        payments: i.payments.map((p, k) => ({ id: `${i.id}-p${k}`, amount: p.amount, method: p.method, paidAt: p.at, collector: '' })),
        refunds: i.refunds.map((r, k) => ({ id: `${i.id}-r${k}`, amount: r.amount, method: r.method, refundedAt: r.at, reason: '', by: '' })),
        cancelled: i.status === 'cancelled' || undefined,
        qr: i.qr ? { bank: i.qr.bankName, account: i.qr.account, holder: i.qr.holder, content: i.qr.content } : undefined,
        transferContent: i.qr?.content,
      }),
    ),
  });
}

/** Sau ghi danh / chuyển lớp / cho nghỉ: server lập / hủy hóa đơn ở nền → tải lại sau chốc lát */
export function refreshInvoicesSoon() {
  setTimeout(() => void loadInvoices().catch(() => undefined), 1500);
}

/** Chờ hóa đơn của ghi danh vừa tạo (lập nền, thường dưới 1 giây) — dùng khi thu tiền ngay lúc chốt lead */
export async function waitForInvoice(enrollmentId: string, studentId: string): Promise<Invoice | undefined> {
  for (let i = 0; i < 12; i++) {
    const list = await optional(api<ServerInvoice[]>(`/finance/invoices?studentId=${toGuid(studentId)}`));
    if (!list) return undefined;
    const hit = list.find((x) => fromGuid(x.enrollmentId) === enrollmentId);
    if (hit) return upsertInvoice(hit);
    await new Promise((r) => setTimeout(r, 500));
  }
  return undefined;
}

// ---------- Hóa đơn (TC-06 → TC-08) ----------

/** TC-06 — thu tiền; Idempotency-Key chống bấm 2 lần */
export async function recordPayment(invoiceId: string, p: { amount: number; method: PaymentMethod; paidAt?: string; note?: string }) {
  const r = await api<ServerInvoice>(`/finance/invoices/${toGuid(invoiceId)}/payments`, {
    body: { amount: p.amount, method: p.method, paidAt: p.paidAt ?? null, note: p.note ?? null },
    headers: { 'Idempotency-Key': crypto.randomUUID() },
  });
  return upsertInvoice(r);
}

/** Phiếu duyệt vừa mở (null = đã ghi ngay, không cần duyệt) */
export type ApprovalTicket = { id: string; code: string } | null;

async function afterApprovalOpened(r: ServerActionResult): Promise<ApprovalTicket> {
  upsertInvoice(r.invoice);
  if (r.approval) await import('@/features/system/systemApi').then((m) => m.loadApprovals()).catch(() => undefined);
  return r.approval ?? null;
}

/** TC-07 — hoàn tiền: từ ngưỡng Cài đặt → mở phiếu ở hộp duyệt chung */
export async function requestRefund(invoiceId: string, p: { amount: number; method: PaymentMethod; reason: string }) {
  const r = await api<ServerActionResult>(`/finance/invoices/${toGuid(invoiceId)}/refunds`, {
    body: p,
    headers: { 'Idempotency-Key': crypto.randomUUID() },
  });
  return afterApprovalOpened(r);
}

/** TC-08 — đề nghị hủy hóa đơn (luôn chờ người thứ 2 duyệt) */
export async function requestVoid(invoiceId: string, reason: string) {
  return afterApprovalOpened(await api<ServerActionResult>(`/finance/invoices/${toGuid(invoiceId)}/void-requests`, { body: { reason } }));
}

/** Sau khi phiếu hoàn / hủy được duyệt / từ chối / rút lại: hóa đơn đổi ở server → tải lại */
export const afterInvoiceDecision = () => loadInvoices().catch(() => undefined);

// ---------- Bảng giá / ưu đãi / đóng đợt (TC-01 → TC-03) ----------

export async function setPrice(courseId: string, branchId: string, fee: number | null) {
  await api('/finance/branch-prices', { method: 'PUT', body: { courseId: toGuid(courseId), branchId: toGuid(branchId), fee } });
  useFinanceStore.setState((s) => ({
    prices: [
      ...s.prices.filter((p) => !(p.courseId === courseId && p.branchId === branchId)),
      ...(fee === null ? [] : [{ courseId, branchId, fee }]),
    ],
  }));
}

/** Tạo / sửa ưu đãi; <code>active</code> khác hiện tại → bật / tắt sau khi lưu */
export async function savePromotion(p: Omit<Promotion, 'id' | 'used' | 'active'> & { id?: string; active?: boolean }) {
  const body = {
    code: p.code,
    name: p.name,
    kind: p.kind,
    value: p.value,
    courseIds: toGuids(p.courseIds),
    branchIds: toGuids(p.branchIds),
    from: p.from,
    to: p.to,
    limit: p.limit ?? null,
    note: p.note ?? null,
  };
  const r = p.id
    ? await api<ServerDiscount>(`/finance/discounts/${p.id}`, { method: 'PUT', body })
    : await api<ServerDiscount>('/finance/discounts', { body });
  const promo = toPromotion(r);
  useFinanceStore.setState((s) => ({ promotions: [...s.promotions.filter((x) => x.id !== promo.id), promo] }));
  if (p.active !== undefined && p.active !== promo.active) await togglePromotion(promo.id, p.active);
}

export async function togglePromotion(id: string, active: boolean) {
  const promo = toPromotion(await api<ServerDiscount>(`/finance/discounts/${id}/toggle`, { body: { active } }));
  useFinanceStore.setState((s) => ({ promotions: s.promotions.map((x) => (x.id === id ? promo : x)) }));
}

export async function savePlan(p: Omit<InstallmentPlan, 'id'> & { id?: string }) {
  const body = { name: p.name, parts: p.parts, minFee: p.minFee, surcharge: p.surcharge, active: p.active };
  const r = p.id
    ? await api<ServerPlan>(`/finance/installment-plans/${p.id}`, { method: 'PUT', body })
    : await api<ServerPlan>('/finance/installment-plans', { body });
  const plan = toPlan(r);
  useFinanceStore.setState((s) => ({ plans: [...s.plans.filter((x) => x.id !== plan.id), plan] }));
}

// ---------- Đối soát VietQR (TC-10) ----------

function upsertTxn(t: ServerTxn) {
  const txn = toTxn(t);
  useFinanceStore.setState((s) => ({ txns: s.txns.map((x) => (x.id === txn.id ? txn : x)) }));
  return txn;
}

/** Khớp tay vào 1 hóa đơn (cả số tiền giao dịch) → khoản thu VietQR; tải lại hóa đơn */
export async function matchTxn(txnId: string, invoiceId: string) {
  upsertTxn(await api<ServerTxn>(`/finance/bank-transactions/${txnId}/match`, { body: { invoiceId: toGuid(invoiceId) } }));
  await loadInvoices();
}

export async function ignoreTxn(txnId: string, note: string) {
  upsertTxn(await api<ServerTxn>(`/finance/bank-transactions/${txnId}/ignore`, { body: { note } }));
}

/** Bỏ khớp → xóa khoản thu tương ứng trên hóa đơn */
export async function unmatchTxn(txnId: string) {
  upsertTxn(await api<ServerTxn>(`/finance/bank-transactions/${txnId}/unmatch`, { method: 'POST' }));
  await loadInvoices();
}

// ---------- Công nợ & nhắc (TC-11) ----------

/** Nhắc hàng loạt (1 request) → cảnh báo (đã nhắc hôm nay, bỏ qua hóa đơn hết nợ) */
export async function sendReminders(invoiceIds: string[], channel: ReminderChannel, note?: string): Promise<string[]> {
  const r = await api<{ sent: number; warnings: string[] }>('/finance/reminders', {
    body: { invoiceIds: toGuids(invoiceIds), channel, note: note ?? null },
  });
  await loadInvoices();
  return r.warnings;
}
