import dayjs from 'dayjs';
import type { LeadStudentLink, Student } from '@/features/crm/types';
import type { ClassInfo } from '@/features/academic/classes/types';
import type { Enrollment, EnrollmentStatus, Invoice, Payment, PortalAccount, Refund } from '@/features/student/types';
import { MOCK_CLASSES } from './classes';

/**
 * Dữ liệu giả — Học viên, lớp, ghi danh, học phí, tài khoản.
 * Có sẵn tình huống: đang học, bảo lưu (không ẩn được), đã hoàn thành / đã nghỉ / chưa xếp lớp (ẩn được),
 * nợ quá hạn, đóng một phần, tài khoản bị khóa, học viên chưa có tài khoản, phụ huynh có / chưa có tài khoản.
 */

const d = (days: number) => dayjs().add(days, 'day').format('YYYY-MM-DD');
const ago = (days: number) => dayjs().subtract(days, 'day').hour(9).minute(0).second(0).toISOString();

// ---------------- Lớp (mocks/data/classes.ts) ----------------
export { MOCK_CLASSES } from './classes';

const classesOf = (branchId: string, status: ClassInfo['status']) => MOCK_CLASSES.filter((c) => c.branchId === branchId && c.status === status);

// ---------------- Học viên ----------------
const special: Student[] = [
  { id: 'ST-1', code: 'HV1000', fullName: 'Nguyễn Văn An', gender: 'male', dob: '2008-04-12', phone: '0912000111', address: '25 Trần Thái Tông, Cầu Giấy, Hà Nội', branchId: 'cg', joinedAt: ago(320), hidden: false, note: 'Mục tiêu JLPT N3 tháng 12. Liên hệ qua bố (anh Bình).' },
  { id: 'ST-2', code: 'HV1001', fullName: 'Nguyễn Gia Bảo', gender: 'male', dob: '2016-09-03', address: '25 Trần Thái Tông, Cầu Giấy, Hà Nội', branchId: 'cg', joinedAt: ago(45), hidden: false, note: 'Em của An — ưu đãi anh chị em 10%.' },
  { id: 'ST-3', code: 'HV1002', fullName: 'Lê Hoàng Phúc', gender: 'male', dob: '1997-11-20', phone: '0901112233', email: 'phuc.le@outlook.com', address: '12 Nguyễn Huệ, Quận 1, TP.HCM', branchId: 'q1', joinedAt: ago(290), hidden: false },
  { id: 'ST-4', code: 'HV1003', fullName: 'Đỗ Minh Tuấn', gender: 'male', dob: '2002-02-14', phone: '0977888999', branchId: 'hc', joinedAt: ago(80), hidden: false, note: 'Đơn hàng thực phẩm Aichi, dự kiến xuất cảnh tháng 3.' },
  { id: 'ST-5', code: 'HV1004', fullName: 'Hoàng Thu Giang', gender: 'female', dob: '2001-07-30', phone: '0966222333', email: 'giang.ht@gmail.com', branchId: 'cg', joinedAt: ago(270), hidden: false, note: 'Bảo lưu 2 tháng do đi công tác.' },
];

const FIRST = ['Nguyễn', 'Trần', 'Lê', 'Phạm', 'Hoàng', 'Vũ', 'Đặng', 'Bùi', 'Đỗ', 'Ngô'];
const MIDDLE = ['Văn', 'Thị', 'Minh', 'Đức', 'Thu', 'Ngọc', 'Hải', 'Quang'];
const LAST = ['Anh', 'Bích', 'Cường', 'Duy', 'Hạnh', 'Hiếu', 'Lan', 'Long', 'My', 'Nhung', 'Sơn', 'Thảo', 'Vinh', 'Xuân'];
const BRANCHES = ['cg', 'q1', 'hc'];

/** Kịch bản học của học viên sinh tự động (theo i % 6) */
type Plan = 'none' | 'completed' | 'studying' | 'completed+studying' | 'dropped' | 'reserved';
const PLANS: Plan[] = ['studying', 'completed', 'completed+studying', 'none', 'studying', 'dropped', 'reserved', 'studying'];

const generated = Array.from({ length: 31 }, (_, i) => {
  const n = i + 5;
  const plan = PLANS[i % PLANS.length];
  const female = MIDDLE[(i * 3) % MIDDLE.length] === 'Thị' || i % 3 === 1;
  const student: Student = {
    id: `ST-${n + 1}`,
    code: `HV${1000 + n}`,
    fullName: `${FIRST[(i * 7) % FIRST.length]} ${MIDDLE[(i * 3) % MIDDLE.length]} ${LAST[(i * 5) % LAST.length]}`,
    gender: female ? 'female' : 'male',
    dob: dayjs('1995-01-01').add(i * 211, 'day').format('YYYY-MM-DD'),
    phone: `03${String(30000000 + i * 271829).slice(0, 8)}`,
    email: i % 3 === 0 ? `hocvien${n}@gmail.com` : undefined,
    branchId: BRANCHES[i % 3],
    joinedAt: ago(20 + i * 11),
    // Một số học viên đã nghỉ / hoàn thành được ẩn khỏi danh sách
    hidden: (plan === 'dropped' || plan === 'completed') && Math.floor(i / 8) % 2 === 1,
  };
  if (student.hidden) student.hiddenAt = ago(5 + i);
  return { student, plan, i };
});

export const MOCK_STUDENTS: Student[] = [...special, ...generated.map((g) => g.student)];

// ---------------- Ghi danh ----------------
let eSeq = 0;
const enroll = (studentId: string, classId: string, status: EnrollmentStatus, note?: string): Enrollment => {
  const c = MOCK_CLASSES.find((x) => x.id === classId)!;
  const start = c.actualStartDate ?? c.plannedStartDate;
  const ended =
    status === 'completed' ? (c.actualEndDate ?? c.plannedEndDate) : status === 'dropped' || status === 'transferred' ? dayjs(start).add(40, 'day').format('YYYY-MM-DD') : undefined;
  const at = dayjs(start).subtract(7 + ((eSeq + 1) % 9), 'day');
  // Lớp sắp mở: ghi danh trong vài ngày gần đây (không để ngày tương lai)
  const enrolledAt = at.isAfter(dayjs().subtract(1, 'day')) ? dayjs().subtract(1 + ((eSeq + 1) % 6), 'day').hour(10) : at;
  return { id: `E-${++eSeq}`, studentId, classId, status, enrolledAt: enrolledAt.toISOString(), endedAt: ended, note };
};

export const MOCK_ENROLLMENTS: Enrollment[] = [
  enroll('ST-1', 'C-N4K11', 'completed'),
  enroll('ST-1', 'C-N3K12', 'studying'),
  enroll('ST-2', 'C-KIDS05', 'studying'),
  enroll('ST-3', 'C-N3K09', 'completed', 'Đỗ JLPT N3 tháng 7'),
  enroll('ST-3', 'C-N2K05', 'studying'),
  enroll('ST-4', 'C-XKLD08', 'studying'),
  enroll('ST-5', 'C-N5K18', 'completed'),
  enroll('ST-5', 'C-N4K15', 'reserved', 'Bảo lưu đến hết tháng sau — đi công tác'),
  // Lớp đang chờ khai giảng (chưa đủ học viên)
  enroll('ST-9', 'C-N5K21', 'studying'),
  enroll('ST-28', 'C-N5K21', 'studying'),
  enroll('ST-20', 'C-N3K13', 'studying'),
  ...generated.flatMap(({ student, plan, i }) => {
    const ongoing = classesOf(student.branchId, 'ongoing');
    const finished = classesOf(student.branchId, 'finished');
    const on = ongoing[i % ongoing.length].id;
    const fin = finished[i % finished.length].id;
    switch (plan) {
      case 'studying':
        return [enroll(student.id, on, 'studying')];
      case 'completed':
        return [enroll(student.id, fin, 'completed')];
      case 'completed+studying':
        return [enroll(student.id, fin, 'completed'), enroll(student.id, on, 'studying')];
      case 'dropped':
        return [enroll(student.id, on, 'dropped', 'Nghỉ do chuyển công tác')];
      case 'reserved':
        return [enroll(student.id, on, 'reserved', 'Bảo lưu vì lý do sức khỏe')];
      default:
        return [];
    }
  }),
];

// Chứng nhận đã cấp cho một phần học viên hoàn thành (dữ liệu giả)
MOCK_ENROLLMENTS.filter((e) => e.status === 'completed')
  .filter((_, i) => i % 3 !== 2)
  .forEach((e, i) => {
    const c = MOCK_CLASSES.find((x) => x.id === e.classId)!;
    const st = MOCK_STUDENTS.find((x) => x.id === e.studentId);
    e.certificate = {
      code: `CN-${dayjs(e.endedAt).year()}-${c.code.replace(/^GV[VN]*_/, '').replace(/[^A-Z0-9]/gi, '')}-${String(i + 1).padStart(4, '0')}`,
      issuedAt: dayjs(e.endedAt).add(5 + (i % 6), 'day').toISOString(),
      sentTo: st?.email ?? `${st?.code.toLowerCase()}@gmail.com`,
      sentBy: 'Phạm Thị Hồng (Giáo vụ)',
      sendCount: i % 4 === 1 ? 2 : 1,
      exception: i === 3 ? 'Vắng 3 buổi do nằm viện — Giám đốc duyệt cấp' : undefined,
    };
  });

// ---------------- Học phí ----------------
const pay = (id: string, amount: number, daysAgo: number, method: Payment['method'] = 'transfer'): Payment => ({
  id,
  amount,
  method,
  paidAt: ago(daysAgo),
  collector: method === 'vietqr' ? 'Hệ thống (VietQR)' : 'Ngô Thị Lan (Kế toán)',
});

const refund = (id: string, amount: number, daysAgo: number): Refund => ({
  id,
  amount,
  method: 'transfer',
  refundedAt: ago(daysAgo),
  reason: 'Nghỉ học giữa khóa — hoàn phần học phí các buổi chưa học',
  by: 'Ngô Thị Lan (Kế toán)',
});

export const MOCK_INVOICES: Invoice[] = MOCK_ENROLLMENTS.map((e, idx) => {
  const c = MOCK_CLASSES.find((x) => x.id === e.classId)!;
  const discount = e.studentId === 'ST-2' ? Math.round(c.fee * 0.1) : idx % 5 === 0 ? 500_000 : 0;
  const total = c.fee - discount;
  const issued = dayjs(e.enrolledAt);
  const half = Math.round(total / 2);
  // Kịch bản thu: lớp đã xong → đã thu đủ; lớp đang học → xoay vòng đủ / 1 phần / chưa thu / quá hạn
  const scenario = e.status === 'completed' ? 'paid' : (['paid', 'partial', 'overdue', 'paid', 'unpaid'] as const)[idx % 5];
  const payments: Payment[] =
    scenario === 'paid'
      ? idx % 2
        ? [pay(`P-${idx}-1`, total, Math.max(1, dayjs().diff(issued, 'day') - 2), 'vietqr')]
        : [pay(`P-${idx}-1`, half, dayjs().diff(issued, 'day') - 1, 'cash'), pay(`P-${idx}-2`, total - half, Math.max(1, dayjs().diff(issued, 'day') - 30))]
      : scenario === 'partial' || scenario === 'overdue'
        ? [pay(`P-${idx}-1`, half, Math.max(1, dayjs().diff(issued, 'day') - 1), 'cash')]
        : [];
  return {
    id: `INV-${idx + 1}`,
    code: `HD${String(2400 + idx)}`,
    enrollmentId: e.id,
    amount: c.fee,
    discount,
    issuedAt: issued.toISOString(),
    // Quá hạn: hạn đóng đợt 2 đã qua; còn lại: hạn trong tương lai
    dueDate: scenario === 'overdue' ? d(-12) : e.status === 'completed' ? dayjs(c.actualStartDate ?? c.plannedStartDate).add(30, 'day').format('YYYY-MM-DD') : d(10 + (idx % 20)),
    payments,
    // Học viên nghỉ: hủy phần học phí còn lại; một nửa số ca được hoàn tiền phần chưa học
    cancelled: e.status === 'dropped' ? true : undefined,
    refunds: e.status === 'dropped' && idx % 2 === 0 && payments.length ? [refund(`R-${idx}`, Math.round(payments[0].amount * 0.6), 30)] : [],
  };
});

// ---------------- Người thân (bổ sung link cho học viên sinh tự động) ----------------
/** Lead đã chốt trong dữ liệu CRM (status "won") đóng vai phụ huynh của một số học viên */
export const EXTRA_LINKS: LeadStudentLink[] = [
  { leadId: 'L-0011', studentId: 'ST-6', relationship: 'child', createdAt: ago(60), primary: true, billing: true, notify: true },
  { leadId: 'L-0018', studentId: 'ST-9', relationship: 'child', createdAt: ago(90), primary: true, billing: true, notify: true },
  { leadId: 'L-0025', studentId: 'ST-9', relationship: 'sibling', createdAt: ago(80), notify: true },
  { leadId: 'L-0032', studentId: 'ST-13', relationship: 'spouse', createdAt: ago(40), notify: true },
  { leadId: 'L-0039', studentId: 'ST-18', relationship: 'child', createdAt: ago(30), primary: true, billing: true, notify: true },
  // Chưa đặt người liên hệ chính → trang Phụ huynh hiện cảnh báo
  { leadId: 'L-0046', studentId: 'ST-21', relationship: 'other', createdAt: ago(50) },
];

// ---------------- Tài khoản ----------------
const hasClass = new Set(MOCK_ENROLLMENTS.map((e) => e.studentId));

export const MOCK_ACCOUNTS: PortalAccount[] = [
  ...MOCK_STUDENTS.filter((s) => hasClass.has(s.id)).map((s, i): PortalAccount => ({
    id: `ACC-S-${s.id}`,
    ownerType: 'student',
    ownerId: s.id,
    username: s.code.toLowerCase(),
    email: s.email,
    status: s.hidden ? 'inactive' : i % 11 === 6 ? 'locked' : 'active',
    createdAt: s.joinedAt,
    lastLoginAt: i % 4 === 3 ? undefined : dayjs().subtract(i * 7 + 2, 'hour').toISOString(),
  })),
  // Phụ huynh
  { id: 'ACC-G-1', ownerType: 'guardian', ownerId: 'L-0001', username: 'ph.0912345678', email: 'binh.nguyen@gmail.com', status: 'active', createdAt: ago(44), lastLoginAt: dayjs().subtract(1, 'day').toISOString() },
  { id: 'ACC-G-2', ownerType: 'guardian', ownerId: 'L-0018', username: 'ph.lead0018', status: 'active', createdAt: ago(85), lastLoginAt: dayjs().subtract(6, 'day').toISOString() },
  { id: 'ACC-G-3', ownerType: 'guardian', ownerId: 'L-0039', username: 'ph.lead0039', status: 'locked', createdAt: ago(28) },
];
