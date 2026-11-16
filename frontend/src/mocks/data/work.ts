import dayjs from 'dayjs';
import type { ShiftKey } from '@/features/hr/types';
import type { Handover, ShiftAssignment, ShiftAvailability, ShiftPreference, Task } from '@/features/work/types';

/**
 * Dữ liệu giả — Công việc: lịch ca (2 tuần trước → tuần này đã xếp; tuần sau mới xếp một phần + có đăng ký ca),
 * task (quá hạn, chờ duyệt, lặp lại, gắn lead / lớp), bàn giao ca (chờ nhận, lệch tiền).
 */

const wd = (d: dayjs.Dayjs) => ((d.day() + 6) % 7) + 1;
const weekStart = dayjs().subtract(wd(dayjs()) - 1, 'day').startOf('day');
const ago = (n: number, h = 9) => dayjs().subtract(n, 'day').hour(h).minute(0).second(0).toISOString();
const d = (n: number) => dayjs().add(n, 'day').format('YYYY-MM-DD');

/** Mẫu ca cố định theo thứ (1 = T2) */
const PATTERNS: Record<string, Partial<Record<number, ShiftKey[]>>> = {
  'e-bao': { 1: ['evening'], 2: ['evening'], 3: ['evening'], 4: ['evening'], 5: ['evening'], 6: ['morning', 'afternoon'] },
  'e-tung': { 1: ['afternoon'], 2: ['afternoon', 'evening'], 3: ['afternoon'], 4: ['afternoon', 'evening'], 5: ['afternoon'], 7: ['morning'] },
  'e-nhu': { 1: ['morning', 'afternoon'], 3: ['morning', 'afternoon'], 5: ['morning', 'afternoon'], 6: ['afternoon'], 7: ['afternoon'] },
  'e-hang': { 1: ['morning', 'afternoon'], 2: ['morning', 'afternoon'], 3: ['morning', 'afternoon'], 4: ['morning', 'afternoon'], 5: ['morning', 'afternoon'], 6: ['morning'] },
  'e-khoa': { 1: ['evening'], 2: ['afternoon', 'evening'], 3: ['evening'], 4: ['afternoon', 'evening'], 5: ['evening'], 6: ['evening'] },
  'e-han': { 1: ['morning', 'afternoon'], 2: ['morning', 'afternoon'], 3: ['morning', 'afternoon'], 4: ['morning', 'afternoon'], 5: ['morning', 'afternoon'], 6: ['afternoon'] },
  'e-vy': { 1: ['morning', 'afternoon'], 2: ['morning'], 3: ['morning', 'afternoon'], 4: ['morning'], 5: ['morning', 'afternoon'], 6: ['morning'] },
};

const shifts: ShiftAssignment[] = [];
const prefs: ShiftPreference[] = [];
let n = 0;
for (let i = -14; i < 14; i++) {
  const day = weekStart.add(i, 'day');
  const date = day.format('YYYY-MM-DD');
  const nextWeek = i >= 7;
  Object.entries(PATTERNS).forEach(([emp, p]) => {
    (p[wd(day)] ?? []).forEach((shift) => {
      // Tuần sau: mới xếp cho Bảo + Hằng; người khác mới ĐĂNG KÝ (chờ quản lý xếp)
      if (nextWeek && !['e-bao', 'e-hang'].includes(emp)) prefs.push({ employeeId: emp, date, shift });
      else shifts.push({ id: `SH-${++n}`, employeeId: emp, date, shift });
    });
  });
}
export const MOCK_SHIFTS = shifts;
export const MOCK_SHIFT_PREFS = prefs;
export const MOCK_SHIFT_AVAILABILITY: ShiftAvailability[] = [
  ...prefs.map((preference) => ({ ...preference, status: 'available' as const })),
  { employeeId: 'e-bao', date: d(7), shift: 'morning', status: 'busy' },
  { employeeId: 'e-hang', date: d(8), shift: 'evening', status: 'busy' },
  { employeeId: 'e-tung', date: d(7), shift: 'morning', status: 'busy' },
  { employeeId: 'e-vy', date: d(7), shift: 'evening', status: 'busy' },
];

export const MOCK_TASKS: Task[] = [
  { id: 'TK-1', code: 'TK-0101', title: 'Gọi lại 5 lead hẹn test xếp lớp tuần này', description: 'Danh sách lead trạng thái "Hẹn test" của chi nhánh Cầu Giấy — xác nhận giờ test, nhắc mang CCCD.', assigneeId: 'e-bao', creatorId: 'e-nam', watcherIds: ['e-mai'], priority: 'high', status: 'doing', due: d(1), repeat: 'none', checklist: [{ id: 'c1', text: 'Lọc lead hẹn test', done: true }, { id: 'c2', text: 'Gọi xác nhận', done: false }, { id: 'c3', text: 'Cập nhật lịch test', done: false }], comments: [{ at: ago(1, 15), by: 'Đinh Quốc Bảo', text: 'Đã gọi 2/5, 3 người còn lại hẹn tối nay.' }], createdAt: ago(3) },
  { id: 'TK-2', code: 'TK-0102', title: 'Gọi phụ huynh học viên vắng 2 buổi liên tiếp lớp N3-K12', assigneeId: 'e-hong', creatorId: 'e-nam', watcherIds: [], priority: 'urgent', status: 'todo', due: d(-1), repeat: 'none', link: { type: 'class', id: 'C-N3K12', label: 'Lớp GVV_N3-K12' }, checklist: [], comments: [], createdAt: ago(2) },
  { id: 'TK-3', code: 'TK-0103', title: 'Đăng bài khai giảng N5-K21 lên Fanpage + chạy quảng cáo', assigneeId: 'e-baoanh', creatorId: 'e-trang', watcherIds: ['e-nam'], priority: 'normal', status: 'review', due: d(2), repeat: 'none', link: { type: 'class', id: 'C-N5K21', label: 'Lớp GVV_N5-K21' }, checklist: [{ id: 'c1', text: 'Thiết kế banner', done: true }, { id: 'c2', text: 'Viết nội dung', done: true }, { id: 'c3', text: 'Duyệt ngân sách 3tr', done: false }], comments: [{ at: ago(0, 10), by: 'Ngô Bảo Anh', text: 'Đã xong bản nháp, nhờ chị Trang duyệt.' }], createdAt: ago(5) },
  { id: 'TK-4', code: 'TK-0104', title: 'Kiểm tra quỹ tiền mặt cuối ngày', assigneeId: 'e-lan2', creatorId: 'e-lan', watcherIds: [], priority: 'normal', status: 'todo', due: d(0), repeat: 'daily', checklist: [], comments: [], createdAt: ago(60) },
  { id: 'TK-5', code: 'TK-0105', title: 'Báo cáo tuyển sinh tuần gửi Ban giám đốc', assigneeId: 'e-nam', creatorId: 'e-huy', watcherIds: ['e-trang'], priority: 'normal', status: 'todo', due: d(3), repeat: 'weekly', checklist: [], comments: [], createdAt: ago(90) },
  { id: 'TK-6', code: 'TK-0106', title: 'Sửa máy chiếu P.301 (báo hỏng của GV Thu Hà)', assigneeId: 'e-tuan', creatorId: 'e-nam', watcherIds: ['t-ha'], priority: 'high', status: 'doing', due: d(-2), repeat: 'none', checklist: [{ id: 'c1', text: 'Kiểm tra cáp HDMI', done: true }, { id: 'c2', text: 'Đặt bóng đèn máy chiếu', done: false }], comments: [], createdAt: ago(4) },
  { id: 'TK-7', code: 'TK-0107', title: 'Chăm sóc lead Võ Thanh Tâm (quan tâm Kaiwa)', assigneeId: 'e-nhu', creatorId: 'e-bao', watcherIds: [], priority: 'normal', status: 'todo', due: d(0), repeat: 'none', link: { type: 'lead', id: 'L-0005', label: 'Lead LD0005 — Võ Thanh Tâm' }, checklist: [], comments: [{ at: ago(0, 8), by: 'Đinh Quốc Bảo', text: 'Em gọi trong ca sáng giúp anh nhé, khách chỉ nghe máy buổi sáng.' }], createdAt: ago(1) },
  { id: 'TK-8', code: 'TK-0108', title: 'Gửi nhắc học phí đợt 2 cho lớp N4-K15', assigneeId: 'e-lan2', creatorId: 'e-lan', watcherIds: [], priority: 'normal', status: 'done', due: d(-3), repeat: 'none', link: { type: 'class', id: 'C-N4K15', label: 'Lớp GVV_N4-K15' }, checklist: [], comments: [], createdAt: ago(8), completedAt: ago(3, 16) },
  { id: 'TK-9', code: 'TK-0109', title: 'Chuẩn bị phòng & tài liệu test xếp lớp Chủ nhật', assigneeId: 'e-hang', creatorId: 'e-mai', watcherIds: [], priority: 'low', status: 'todo', due: d(4), repeat: 'none', checklist: [{ id: 'c1', text: 'In 20 đề test', done: false }, { id: 'c2', text: 'Mượn P.401', done: false }], comments: [], createdAt: ago(1) },
  // Task của các tài khoản demo (để "Việc của tôi" / "Giao việc cấp dưới" có dữ liệu)
  { id: 'TK-11', code: 'TK-0111', title: 'Duyệt kế hoạch tuyển sinh Quý 1/2027', description: 'Kế hoạch ngân sách quảng cáo + chỉ tiêu từng chi nhánh, chốt trước họp giao ban.', assigneeId: 'e-huy', creatorId: 'e-trang', watcherIds: ['e-baoanh'], priority: 'high', status: 'todo', due: d(2), repeat: 'none', checklist: [{ id: 'c1', text: 'Xem ngân sách quảng cáo', done: false }, { id: 'c2', text: 'Chốt chỉ tiêu 3 chi nhánh', done: false }], comments: [], createdAt: ago(1) },
  { id: 'TK-12', code: 'TK-0112', title: 'Ký duyệt bảng lương tháng 10', assigneeId: 'e-huy', creatorId: 'e-lan', watcherIds: [], priority: 'urgent', status: 'todo', due: d(-1), repeat: 'none', checklist: [], comments: [{ at: ago(1, 9), by: 'Hoàng Thị Lan', text: 'Bảng lương đã chốt ở Tài chính, nhờ anh duyệt để chuyển khoản.' }], createdAt: ago(2) },
  { id: 'TK-13', code: 'TK-0113', title: 'Gọi 3 lead quá SLA chi nhánh Quận 1', assigneeId: 'e-khoa', creatorId: 'e-q1m', watcherIds: [], priority: 'urgent', status: 'todo', due: d(0), repeat: 'none', checklist: [{ id: 'c1', text: 'Phạm Gia Huy', done: true }, { id: 'c2', text: 'Lead form website', done: false }, { id: 'c3', text: 'Lead Facebook', done: false }], comments: [], createdAt: ago(0, 8) },
  { id: 'TK-14', code: 'TK-0114', title: 'Cập nhật bảng giá khóa N3 trên Zalo OA', assigneeId: 'e-khoa', creatorId: 'e-q1m', watcherIds: [], priority: 'low', status: 'doing', due: d(5), repeat: 'none', checklist: [], comments: [], createdAt: ago(2) },
  { id: 'TK-15', code: 'TK-0115', title: 'Đối chiếu công nợ quá hạn > 30 ngày', assigneeId: 'e-lan', creatorId: 'e-huy', watcherIds: [], priority: 'high', status: 'todo', due: d(1), repeat: 'monthly', checklist: [], comments: [], createdAt: ago(1) },
  { id: 'TK-10', code: 'TK-0110', title: 'Tổng hợp hồ sơ JLPT 12/2026 còn thiếu', assigneeId: 'e-mai', creatorId: 'e-nam', watcherIds: [], priority: 'high', status: 'done', due: d(-10), repeat: 'none', checklist: [], comments: [], createdAt: ago(20), completedAt: ago(11) },
];

const today = dayjs().format('YYYY-MM-DD');
export const MOCK_HANDOVERS: Handover[] = [
  {
    id: 'HO-1',
    branchId: 'cg',
    date: today,
    fromShift: 'afternoon',
    fromId: 'e-tung',
    toId: 'e-bao',
    cash: { expected: 4_550_000, counted: 4_550_000 },
    leads: [
      { leadId: 'L-0005', note: 'Hẹn gọi lại 19h về lớp Kaiwa tối' },
      { leadId: 'L-0011', note: 'Đã gửi bảng giá, chờ phụ huynh quyết' },
    ],
    pending: [{ text: 'Khách đặt cọc giữ chỗ lớp N5-K21 sẽ quay lại lấy biên lai lúc 18h' }, { text: 'Hộp thư Fanpage còn 3 tin chưa trả lời' }],
    status: 'pending',
    createdAt: dayjs().hour(17).minute(32).toISOString(),
  },
  {
    id: 'HO-2',
    branchId: 'cg',
    date: dayjs().subtract(1, 'day').format('YYYY-MM-DD'),
    fromShift: 'afternoon',
    fromId: 'e-hang',
    toId: 'e-bao',
    cash: { expected: 2_300_000, counted: 2_250_000, receivedCount: 2_250_000, note: 'Thiếu 50.000đ — trả lại tiền thừa nhầm cho khách' },
    leads: [],
    pending: [{ text: 'Trả CCCD cho học viên Đỗ Văn My (để quên ở quầy)', done: true }],
    issues: 'Máy in quầy kẹt giấy',
    status: 'disputed',
    createdAt: ago(1, 17),
    receivedAt: ago(1, 18),
    receiverNote: 'Nhận đủ 2.250.000đ, lệch 50.000đ so với hệ thống — đã báo kế toán.',
  },
  {
    id: 'HO-3',
    branchId: 'cg',
    date: dayjs().subtract(1, 'day').format('YYYY-MM-DD'),
    fromShift: 'morning',
    fromId: 'e-nhu',
    toId: 'e-tung',
    cash: { expected: 1_000_000, counted: 1_000_000, receivedCount: 1_000_000 },
    leads: [{ leadId: 'L-0004', note: 'Đã gọi 2 lần không nghe máy' }],
    pending: [],
    status: 'received',
    createdAt: ago(1, 12),
    receivedAt: ago(1, 13),
  },
  {
    id: 'HO-4',
    branchId: 'q1',
    date: dayjs().subtract(1, 'day').format('YYYY-MM-DD'),
    fromShift: 'afternoon',
    fromId: 'e-han',
    toId: 'e-khoa',
    cash: { expected: 3_200_000, counted: 3_200_000, receivedCount: 3_200_000 },
    leads: [],
    pending: [{ text: 'Gửi lịch khai giảng N3-K13 cho 2 khách walk-in' }],
    status: 'received',
    createdAt: ago(1, 17),
    receivedAt: ago(1, 17),
  },
];
