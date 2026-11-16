import dayjs from 'dayjs';
import { create } from 'zustand';
import { useStudentStore } from '@/features/student/studentStore';
import {
  MOCK_BANK_TXNS,
  MOCK_BRANCH_PRICES,
  MOCK_COMMISSION_TIERS,
  MOCK_INSTALLMENTS,
  MOCK_PAY_ADJUSTS,
  MOCK_PAYROLL_LOCKS,
  MOCK_PROMOTIONS,
  MOCK_REMINDERS,
  MOCK_SALE_TARGETS,
} from '@/mocks/data/finance';
import type { BankTxn, BranchPrice, CommissionTier, InstallmentPlan, PayAdjust, PayrollLock, Promotion, Reminder, SaleTarget } from './types';
import { moveToTrash, registerRestorer } from '@/features/system/trash';

/** Store TÀI CHÍNH GIẢ LẬP. Hóa đơn / thu / hoàn dùng chung studentStore. */
interface FinanceState {
  prices: BranchPrice[];
  promotions: Promotion[];
  plans: InstallmentPlan[];
  txns: BankTxn[];
  reminders: Reminder[];
  adjusts: PayAdjust[];
  locks: PayrollLock[];
  tiers: CommissionTier[];
  targets: SaleTarget[];
  /** Ghi nhận doanh số hóa đơn cho Sale khác (mặc định suy theo chi nhánh) */
  saleOverrides: Record<string, string>;

  setPrice: (courseId: string, branchId: string, fee: number | null) => void;
  savePromotion: (p: Omit<Promotion, 'id' | 'used'> & { id?: string }) => void;
  togglePromotion: (id: string) => void;
  savePlan: (p: Omit<InstallmentPlan, 'id'> & { id?: string }) => void;

  /** Giao dịch ngân hàng mới (webhook giả lập) */
  receiveTxn: (t: Omit<BankTxn, 'id' | 'status'>) => BankTxn;
  /** Khớp giao dịch với hóa đơn → ghi nhận thanh toán VietQR */
  matchTxn: (txnId: string, invoiceId: string, by: string, auto?: boolean) => void;
  ignoreTxn: (txnId: string, by: string, note: string) => void;
  unmatchTxn: (txnId: string) => void;

  remind: (invoiceIds: string[], channel: Reminder['channel'], by: string, note?: string) => void;

  addAdjust: (a: Omit<PayAdjust, 'id'>) => void;
  removeAdjust: (id: string) => void;
  lockPayroll: (l: Omit<PayrollLock, 'lockedAt'>) => void;
  unlockPayroll: (kind: PayrollLock['kind'], month: string, branchId: string) => void;
  markPaid: (kind: PayrollLock['kind'], month: string, branchId: string) => void;

  setTiers: (t: CommissionTier[]) => void;
  setTarget: (employeeId: string, month: string, target: number) => void;
  assignSale: (invoiceId: string, employeeId: string) => void;
}

const now = () => new Date().toISOString();
const sameLock = (kind: string, month: string, branchId: string) => (l: PayrollLock) => l.kind === kind && l.month === month && l.branchId === branchId;

export const useFinanceStore = create<FinanceState>((set, get) => ({
  prices: MOCK_BRANCH_PRICES,
  promotions: MOCK_PROMOTIONS,
  plans: MOCK_INSTALLMENTS,
  txns: MOCK_BANK_TXNS,
  reminders: MOCK_REMINDERS,
  adjusts: MOCK_PAY_ADJUSTS,
  locks: MOCK_PAYROLL_LOCKS,
  tiers: MOCK_COMMISSION_TIERS,
  targets: MOCK_SALE_TARGETS,
  saleOverrides: {},

  setPrice: (courseId, branchId, fee) =>
    set((s) => ({ prices: [...s.prices.filter((p) => !(p.courseId === courseId && p.branchId === branchId)), ...(fee === null ? [] : [{ courseId, branchId, fee }])] })),
  savePromotion: (p) =>
    set((s) => ({
      promotions: p.id ? s.promotions.map((x) => (x.id === p.id ? { ...x, ...p, id: x.id } : x)) : [{ ...p, id: `PR-${Date.now()}`, used: 0 }, ...s.promotions],
    })),
  togglePromotion: (id) => set((s) => ({ promotions: s.promotions.map((p) => (p.id === id ? { ...p, active: !p.active } : p)) })),
  savePlan: (p) => set((s) => ({ plans: p.id ? s.plans.map((x) => (x.id === p.id ? { ...x, ...p, id: x.id } : x)) : [...s.plans, { ...p, id: `IP-${Date.now()}` }] })),

  receiveTxn: (t) => {
    const txn: BankTxn = { ...t, id: `TX-${Date.now()}`, status: 'unmatched' };
    set((s) => ({ txns: [txn, ...s.txns] }));
    return txn;
  },
  matchTxn: (txnId, invoiceId, by, auto) => {
    const t = get().txns.find((x) => x.id === txnId);
    if (!t) return;
    useStudentStore.getState().addPayment(invoiceId, { amount: t.amount, method: 'vietqr', paidAt: t.at, collector: auto ? 'VietQR (tự khớp)' : `VietQR — ${by} khớp tay` });
    set((s) => ({ txns: s.txns.map((x) => (x.id === txnId ? { ...x, status: auto ? 'auto' : 'matched', invoiceId, matchedBy: by, matchedAt: now() } : x)) }));
  },
  ignoreTxn: (txnId, by, note) => set((s) => ({ txns: s.txns.map((x) => (x.id === txnId ? { ...x, status: 'ignored', note, matchedBy: by, matchedAt: now() } : x)) })),
  unmatchTxn: (txnId) => {
    const t = get().txns.find((x) => x.id === txnId);
    if (!t) return;
    // Gỡ khớp: xóa lần thanh toán tương ứng khỏi hóa đơn
    if (t.invoiceId)
      useStudentStore.setState((s) => ({
        invoices: s.invoices.map((i) => {
          if (i.id !== t.invoiceId) return i;
          const idx = i.payments.findIndex((p) => p.method === 'vietqr' && p.amount === t.amount && p.paidAt === t.at);
          return idx < 0 ? i : { ...i, payments: i.payments.filter((_, k) => k !== idx) };
        }),
      }));
    set((s) => ({ txns: s.txns.map((x) => (x.id === txnId ? { ...x, status: 'unmatched', invoiceId: undefined, matchedBy: undefined, matchedAt: undefined, note: undefined } : x)) }));
  },

  remind: (invoiceIds, channel, by, note) =>
    set((s) => ({ reminders: [...invoiceIds.map((invoiceId, i) => ({ id: `RM-${Date.now()}-${i}`, invoiceId, channel, at: now(), by, note })), ...s.reminders] })),

  addAdjust: (a) => set((s) => ({ adjusts: [...s.adjusts, { ...a, id: `PA-${Date.now()}` }] })),
  removeAdjust: (id) => {
    const a = useFinanceStore.getState().adjusts.find((x) => x.id === id);
    if (a) moveToTrash({ kind: 'pay_adjust', label: `${a.amount > 0 ? '+' : ''}${a.amount.toLocaleString('vi-VN')}đ — ${a.reason}`, detail: `Tháng ${a.month} · ${a.employeeId}`, payload: a });
    set((s) => ({ adjusts: s.adjusts.filter((x) => x.id !== id) }));
  },
  lockPayroll: (l) => set((s) => ({ locks: [...s.locks.filter((x) => !sameLock(l.kind, l.month, l.branchId)(x)), { ...l, lockedAt: now() }] })),
  unlockPayroll: (kind, month, branchId) => set((s) => ({ locks: s.locks.filter((x) => !sameLock(kind, month, branchId)(x)) })),
  markPaid: (kind, month, branchId) => set((s) => ({ locks: s.locks.map((x) => (sameLock(kind, month, branchId)(x) ? { ...x, paidAt: now() } : x)) })),

  setTiers: (tiers) => set({ tiers: [...tiers].sort((a, b) => a.from - b.from) }),
  setTarget: (employeeId, month, target) =>
    set((s) => ({ targets: [...s.targets.filter((t) => !(t.employeeId === employeeId && t.month === month)), { employeeId, month, target }] })),
  assignSale: (invoiceId, employeeId) => set((s) => ({ saleOverrides: { ...s.saleOverrides, [invoiceId]: employeeId } })),
}));

export const monthKey = (d = dayjs()) => d.format('YYYY-MM');

registerRestorer<PayAdjust>('pay_adjust', (a) => useFinanceStore.setState((s) => ({ adjusts: [...s.adjusts, a] })));
