import dayjs from 'dayjs';
import type { BankTxn, BranchPrice, CommissionTier, InstallmentPlan, PayAdjust, PayrollLock, Promotion, Reminder, SaleTarget } from '@/features/finance/types';
import { MOCK_CLASSES } from './classes';
import { MOCK_ENROLLMENTS, MOCK_INVOICES, MOCK_STUDENTS } from './students';

const d = (days: number) => dayjs().add(days, 'day').format('YYYY-MM-DD');
const ago = (days: number, h = 9) => dayjs().subtract(days, 'day').hour(h).minute(15).second(0).toISOString();
const month = (offset = 0) => dayjs().add(offset, 'month').format('YYYY-MM');

/** Bỏ dấu tiếng Việt, viết hoa (giống nội dung CK ngân hàng) */
export const unaccent = (s: string) =>
  s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toUpperCase();

export const MOCK_BRANCH_PRICES: BranchPrice[] = [
  { courseId: 'CRS-01', branchId: 'q1', fee: 5_200_000 },
  { courseId: 'CRS-02', branchId: 'q1', fee: 6_200_000 },
  { courseId: 'CRS-01', branchId: 'hc', fee: 4_500_000 },
  { courseId: 'CRS-13', branchId: 'hc', fee: 3_500_000 },
];

export const MOCK_PROMOTIONS: Promotion[] = [
  { id: 'PR-1', code: 'EARLY10', name: 'Đăng ký sớm trước khai giảng 14 ngày', kind: 'percent', value: 10, courseIds: [], branchIds: ['cg', 'q1', 'hc'], from: d(-60), to: d(45), used: 23, active: true },
  { id: 'PR-2', code: 'N5N4COMBO', name: 'Combo N5 + N4 (lộ trình 6 tháng)', kind: 'combo', value: 1_500_000, courseIds: ['CRS-01', 'CRS-02'], branchIds: ['cg', 'q1', 'hc'], from: d(-90), to: d(90), used: 9, active: true },
  { id: 'PR-3', code: 'BANBE500', name: 'Học viên cũ giới thiệu bạn', kind: 'amount', value: 500_000, courseIds: [], branchIds: ['cg', 'q1', 'hc'], from: d(-200), to: d(160), used: 31, active: true, note: 'Cả người giới thiệu và người được giới thiệu đều được giảm.' },
  { id: 'PR-4', code: 'SV15', name: 'Sinh viên đại học đối tác', kind: 'percent', value: 15, courseIds: ['CRS-03', 'CRS-04'], branchIds: ['cg'], from: d(-30), to: d(60), limit: 40, used: 12, active: true },
  { id: 'PR-5', code: 'HE2026', name: 'Khuyến mãi hè 2026', kind: 'percent', value: 12, courseIds: [], branchIds: ['cg', 'q1', 'hc'], from: d(-170), to: d(-80), used: 58, active: false },
  { id: 'PR-6', code: 'KIDS2', name: 'Anh chị em ruột cùng học thiếu nhi', kind: 'amount', value: 700_000, courseIds: ['CRS-06'], branchIds: ['cg'], from: d(-120), to: d(240), used: 6, active: true },
];

export const MOCK_INSTALLMENTS: InstallmentPlan[] = [
  { id: 'IP-1', name: 'Đóng 1 lần', parts: [{ percent: 100, offsetDays: -3 }], minFee: 0, surcharge: 0, active: true },
  { id: 'IP-2', name: '2 đợt (50% – 50%)', parts: [{ percent: 50, offsetDays: -3 }, { percent: 50, offsetDays: 45 }], minFee: 4_000_000, surcharge: 0, active: true },
  { id: 'IP-3', name: '3 đợt hàng tháng', parts: [{ percent: 40, offsetDays: -3 }, { percent: 30, offsetDays: 30 }, { percent: 30, offsetDays: 60 }], minFee: 6_000_000, surcharge: 3, active: true },
  { id: 'IP-4', name: 'XKLĐ: đóng sau khi có việc', parts: [{ percent: 30, offsetDays: -3 }, { percent: 70, offsetDays: 120 }], minFee: 8_000_000, surcharge: 5, active: false },
];

// ---------- Giao dịch ngân hàng ----------
const studentOfInvoice = (invId: string) => {
  const inv = MOCK_INVOICES.find((i) => i.id === invId)!;
  const e = MOCK_ENROLLMENTS.find((x) => x.id === inv.enrollmentId)!;
  return { inv, st: MOCK_STUDENTS.find((s) => s.id === e.studentId)!, cls: MOCK_CLASSES.find((c) => c.id === e.classId)! };
};

/** Mỗi lần đóng bằng VietQR → 1 giao dịch đã tự khớp */
const autoTxns: BankTxn[] = MOCK_INVOICES.flatMap((inv) =>
  inv.payments
    .filter((p) => p.method === 'vietqr')
    .map((p, i) => {
      const { st, cls } = studentOfInvoice(inv.id);
      return {
        id: `TX-A${inv.id}-${i}`,
        ref: `FT${dayjs(p.paidAt).format('YYMMDD')}${String(4000 + Number(inv.id.replace(/\D/g, '')) * 7).slice(-5)}`,
        at: p.paidAt,
        amount: p.amount,
        content: `${inv.code} ${unaccent(st.fullName)}`,
        payer: unaccent(st.fullName),
        account: cls.branchId,
        branchId: cls.branchId,
        status: 'auto' as const,
        invoiceId: inv.id,
        matchedBy: 'Hệ thống',
        matchedAt: p.paidAt,
      };
    }),
);

/** Giao dịch chưa khớp: ghi sai / thiếu mã hóa đơn — cần xử lý tay */
const owing = MOCK_INVOICES.filter((i) => !i.cancelled && i.payments.length === 0).slice(0, 3);
const looseTxns: BankTxn[] = [
  ...owing.map((inv, i) => {
    const { st, cls } = studentOfInvoice(inv.id);
    const total = inv.amount - inv.discount;
    const contents = [`CK HOC PHI ${unaccent(st.fullName)}`, `${unaccent(st.fullName)} chuyen tien hoc tieng nhat`, `ME CHAU ${unaccent(st.fullName.split(' ').pop()!)} DONG HOC PHI LOP ${cls.code}`];
    return {
      id: `TX-U${i + 1}`,
      ref: `FT${dayjs().subtract(i, 'day').format('YYMMDD')}9${i}21${i}`,
      at: ago(i, 10 + i),
      amount: i === 1 ? Math.round(total / 2) : total,
      content: contents[i % 3],
      payer: unaccent(i === 2 ? 'Nguyễn Thị Lan' : st.fullName),
      account: cls.branchId,
      branchId: cls.branchId,
      status: 'unmatched' as const,
    };
  }),
  { id: 'TX-U9', ref: 'FT2611140099', at: ago(1, 15), amount: 2_450_000, content: 'HD9999 TRAN VAN HUNG', payer: 'TRAN VAN HUNG', account: 'q1', branchId: 'q1', status: 'unmatched', note: 'Mã hóa đơn không tồn tại' },
  { id: 'TX-I1', ref: 'FT2611100321', at: ago(5, 11), amount: 1_200_000, content: 'HOAN UNG MUA VPP T10', payer: 'PHAM THU TRANG', account: 'cg', branchId: 'cg', status: 'ignored', note: 'Không phải học phí — hoàn ứng nội bộ', matchedBy: 'Hoàng Thị Lan', matchedAt: ago(5, 14) },
];

export const MOCK_BANK_TXNS: BankTxn[] = [...looseTxns, ...autoTxns].sort((a, b) => b.at.localeCompare(a.at));

// ---------- Nhắc học phí ----------
export const MOCK_REMINDERS: Reminder[] = MOCK_INVOICES.filter((i) => !i.cancelled && dayjs(i.dueDate).isBefore(dayjs(), 'day') && i.payments.reduce((n, p) => n + p.amount, 0) < i.amount - i.discount).flatMap((inv, i) => [
  { id: `RM-${inv.id}-1`, invoiceId: inv.id, channel: 'zalo' as const, at: ago(10 - (i % 3)), by: 'Hệ thống (tự động)' },
  ...(i % 2 === 0 ? [{ id: `RM-${inv.id}-2`, invoiceId: inv.id, channel: 'call' as const, at: ago(4 - (i % 3), 15), by: 'Phạm Thị Hồng', note: i % 4 === 0 ? 'Phụ huynh hẹn đóng cuối tuần' : 'Không nghe máy' }] : []),
]);

// ---------- Lương ----------
export const MOCK_PAY_ADJUSTS: PayAdjust[] = [
  { id: 'PA-1', month: month(), employeeId: 't-ha', amount: 500_000, reason: 'Thưởng lớp N3-K12 đạt tỷ lệ đỗ thi thử 90%', by: 'Lê Hoàng Nam' },
  { id: 'PA-2', month: month(), employeeId: 't-sato', amount: 300_000, reason: 'Hỗ trợ chấm bài thi thử JLPT', by: 'Lê Hoàng Nam' },
  { id: 'PA-3', month: month(), employeeId: 'e-tung', amount: -200_000, reason: 'Làm mất chìa khóa tủ hồ sơ', by: 'Phạm Thu Trang' },
  { id: 'PA-4', month: month(), employeeId: 'e-hong', amount: 400_000, reason: 'Trực sự kiện ngày hội du học (Chủ nhật)', by: 'Phạm Thu Trang' },
];

export const MOCK_PAYROLL_LOCKS: PayrollLock[] = (['cg', 'q1', 'hc'] as const).flatMap((b) => [
  { kind: 'teacher' as const, month: month(-1), branchId: b, lockedBy: 'Hoàng Thị Lan', lockedAt: dayjs().startOf('month').add(3, 'day').toISOString(), total: 0, paidAt: dayjs().startOf('month').add(4, 'day').toISOString() },
  { kind: 'staff' as const, month: month(-1), branchId: b, lockedBy: 'Hoàng Thị Lan', lockedAt: dayjs().startOf('month').add(3, 'day').toISOString(), total: 0, paidAt: dayjs().startOf('month').add(4, 'day').toISOString() },
]);

// ---------- Hoa hồng ----------
export const MOCK_COMMISSION_TIERS: CommissionTier[] = [
  { from: 0, percent: 3 },
  { from: 5_000_000, percent: 4 },
  { from: 10_000_000, percent: 5 },
  { from: 20_000_000, percent: 6 },
];

export const MOCK_SALE_TARGETS: SaleTarget[] = ['e-khoa', 'e-bao', 'e-vy', 'e-han', 'e-tung', 'e-nhu'].flatMap((id, i) =>
  [-2, -1, 0].map((o) => ({ employeeId: id, month: month(o), target: [12, 8, 10, 9, 7, 5][i] * 1_000_000 })),
);
