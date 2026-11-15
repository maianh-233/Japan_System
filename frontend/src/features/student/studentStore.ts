import dayjs from 'dayjs';
import { create } from 'zustand';
import { useClassStore } from '@/features/academic/classes/classStore';
import { MOCK_ACCOUNTS, MOCK_ENROLLMENTS, MOCK_INVOICES } from '@/mocks/data/students';
import { ACTIVE_ENROLLMENT } from './constants';
import type { AccountStatus, Enrollment, EnrollmentStatus, Invoice, Payment, PortalAccount, Refund, StudyStatus, TuitionStatus } from './types';

/**
 * Store HỌC VIÊN GIẢ LẬP (trong bộ nhớ): ghi danh, hóa đơn (thu tiền / hoàn tiền), tài khoản cổng.
 * Hồ sơ học viên + người thân nằm ở crmStore; lớp học nằm ở classStore.
 * Khi có backend: thay hàm bằng gọi API, giữ nguyên chữ ký.
 */

interface StudentState {
  enrollments: Enrollment[];
  invoices: Invoice[];
  accounts: PortalAccount[];

  /** Ghi danh học viên vào lớp + sinh hóa đơn học phí (giá theo lớp) */
  enroll: (studentId: string, classId: string, opts?: { discount?: number; dueDate?: string }) => void;
  setEnrollmentStatus: (enrollmentId: string, status: EnrollmentStatus, note?: string) => void;
  /** Học viên nghỉ: ghi danh → "Đã nghỉ", hủy phần học phí còn lại */
  dropEnrollment: (enrollmentId: string, reason: string) => void;

  addPayment: (invoiceId: string, payment: Omit<Payment, 'id'>) => void;
  addRefund: (invoiceId: string, refund: Omit<Refund, 'id'>) => void;

  /** Cấp tài khoản cổng (học viên / phụ huynh) */
  issueAccount: (input: Omit<PortalAccount, 'id' | 'createdAt' | 'status'>) => PortalAccount;
  setAccountStatus: (accountId: string, status: AccountStatus) => void;
}

const now = () => new Date().toISOString();

export const useStudentStore = create<StudentState>((set, get) => ({
  enrollments: MOCK_ENROLLMENTS,
  invoices: MOCK_INVOICES,
  accounts: MOCK_ACCOUNTS,

  enroll: (studentId, classId, opts) => {
    const c = useClassStore.getState().classes.find((x) => x.id === classId);
    if (!c) return;
    const e: Enrollment = { id: `E-${Date.now()}`, studentId, classId, status: 'studying', enrolledAt: now() };
    const inv: Invoice = {
      id: `INV-${Date.now()}`,
      code: `HD${2400 + get().invoices.length}`,
      enrollmentId: e.id,
      amount: c.fee,
      discount: opts?.discount ?? 0,
      issuedAt: now(),
      dueDate: opts?.dueDate ?? dayjs().add(7, 'day').format('YYYY-MM-DD'),
      payments: [],
      refunds: [],
    };
    set((s) => ({ enrollments: [...s.enrollments, e], invoices: [...s.invoices, inv] }));
  },

  setEnrollmentStatus: (enrollmentId, status, note) =>
    set((s) => ({
      enrollments: s.enrollments.map((e) =>
        e.id === enrollmentId
          ? { ...e, status, note: note ?? e.note, endedAt: ['completed', 'dropped', 'transferred'].includes(status) ? dayjs().format('YYYY-MM-DD') : undefined }
          : e,
      ),
    })),

  dropEnrollment: (enrollmentId, reason) =>
    set((s) => ({
      enrollments: s.enrollments.map((e) => (e.id === enrollmentId ? { ...e, status: 'dropped', note: reason, endedAt: dayjs().format('YYYY-MM-DD') } : e)),
      invoices: s.invoices.map((i) => (i.enrollmentId === enrollmentId ? { ...i, cancelled: true } : i)),
    })),

  addPayment: (invoiceId, payment) =>
    set((s) => ({ invoices: s.invoices.map((i) => (i.id === invoiceId ? { ...i, payments: [...i.payments, { ...payment, id: `P-${Date.now()}` }] } : i)) })),

  addRefund: (invoiceId, refund) =>
    set((s) => ({ invoices: s.invoices.map((i) => (i.id === invoiceId ? { ...i, refunds: [...i.refunds, { ...refund, id: `R-${Date.now()}` }] } : i)) })),

  issueAccount: (input) => {
    const acc: PortalAccount = { ...input, id: `ACC-${Date.now()}`, status: 'active', createdAt: now() };
    set((s) => ({ accounts: [...s.accounts, acc] }));
    return acc;
  },

  setAccountStatus: (accountId, status) =>
    set((s) => ({ accounts: s.accounts.map((a) => (a.id === accountId ? { ...a, status } : a)) })),
}));

// ---------------- Tính toán (thuần, dùng được ở mọi nơi) ----------------

export interface InvoiceSummary {
  total: number;
  /** Đã thu (tổng các lần đóng) */
  collected: number;
  /** Đã hoàn */
  refunded: number;
  /** Thực thu = đã thu − đã hoàn */
  paid: number;
  remaining: number;
  /** Còn hoàn được tối đa */
  refundable: number;
  status: TuitionStatus;
}

export function summarizeInvoice(inv: Invoice): InvoiceSummary {
  const total = inv.amount - inv.discount;
  const collected = inv.payments.reduce((s, p) => s + p.amount, 0);
  const refunded = inv.refunds.reduce((s, r) => s + r.amount, 0);
  const paid = collected - refunded;
  const remaining = inv.cancelled ? 0 : Math.max(0, total - collected);
  const status: TuitionStatus =
    refunded > 0
      ? 'refunded'
      : inv.cancelled
        ? 'cancelled'
        : remaining === 0
          ? 'paid'
          : dayjs(inv.dueDate).isBefore(dayjs(), 'day')
            ? 'overdue'
            : collected > 0
              ? 'partial'
              : 'unpaid';
  return { total, collected, refunded, paid, remaining, refundable: Math.max(0, paid), status };
}

/** Học phí tổng của học viên: tình trạng xấu nhất + tổng còn nợ */
export function summarizeTuition(invoices: Invoice[]): { status?: TuitionStatus; remaining: number; total: number; paid: number } {
  if (!invoices.length) return { remaining: 0, total: 0, paid: 0 };
  const list = invoices.map(summarizeInvoice);
  const order: TuitionStatus[] = ['overdue', 'unpaid', 'partial', 'paid', 'refunded', 'cancelled'];
  const status = order.find((st) => list.some((x) => x.status === st));
  return {
    status,
    remaining: list.reduce((s, x) => s + x.remaining, 0),
    // Hóa đơn đã hủy / hoàn: chỉ tính phần thực thu
    total: list.reduce((s, x) => s + (x.status === 'refunded' || x.status === 'cancelled' ? x.paid : x.total), 0),
    paid: list.reduce((s, x) => s + x.paid, 0),
  };
}

/** Trạng thái học tổng hợp từ các ghi danh */
export function studyStatusOf(enrollments: Enrollment[]): StudyStatus {
  if (enrollments.some((e) => e.status === 'studying')) return 'studying';
  if (enrollments.some((e) => e.status === 'reserved')) return 'reserved';
  if (enrollments.some((e) => e.status === 'completed')) return 'finished';
  if (enrollments.length) return 'dropped';
  return 'none';
}

/** Các ghi danh còn hiệu lực (đang học / bảo lưu) — còn thì KHÔNG được ẩn */
export const activeEnrollments = (enrollments: Enrollment[]) => enrollments.filter((e) => ACTIVE_ENROLLMENT.includes(e.status));

/** Gom dữ liệu theo studentId để tra cứu nhanh trong bảng */
export function groupByStudent(enrollments: Enrollment[], invoices: Invoice[]) {
  const enr = new Map<string, Enrollment[]>();
  enrollments.forEach((e) => enr.set(e.studentId, [...(enr.get(e.studentId) ?? []), e]));
  const byEnrollment = new Map(enrollments.map((e) => [e.id, e.studentId]));
  const inv = new Map<string, Invoice[]>();
  invoices.forEach((i) => {
    const sid = byEnrollment.get(i.enrollmentId);
    if (sid) inv.set(sid, [...(inv.get(sid) ?? []), i]);
  });
  return { enr, inv };
}
