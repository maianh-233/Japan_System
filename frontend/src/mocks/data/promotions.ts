import dayjs from 'dayjs';
import type { PromotionCandidate } from '@/features/academic/promotion/types';
import { MOCK_ENROLLMENTS } from './students';

/**
 * Dữ liệu giả — LÊN LỚP:
 *  - N3-K12 (đang học) → N2-K06 · KIDS-K05 (đang học) → KIDS-K06: đã giữ chỗ, chưa giao Sale (lớp chưa kết thúc)
 *    → phụ huynh anh Bình (L-0001) có 2 con trong danh sách: An (N3) và bé Gia Bảo (thiếu nhi)
 *  - N5-K18 (đã kết thúc) → N4-K17: đã giao Sale, đang tư vấn
 */

const d = (days: number) => dayjs().add(days, 'day').format('YYYY-MM-DD');
const ago = (days: number, h = 10) => dayjs().subtract(days, 'day').hour(h).minute(0).second(0).toISOString();

const busyElsewhere = (studentId: string, fromClassId: string) =>
  MOCK_ENROLLMENTS.some((e) => e.studentId === studentId && e.classId !== fromClassId && (e.status === 'studying' || e.status === 'reserved'));

const base = (studentId: string, fromClassId: string, toClassId: string, seatHeld: boolean, holdUntil: string): PromotionCandidate => ({
  id: `PR-${fromClassId}-${studentId}`,
  studentId,
  fromClassId,
  toClassId,
  seatHeld,
  holdUntil: seatHeld ? holdUntil : undefined,
  status: 'pending',
  source: 'stats',
  notes: [],
  createdAt: ago(3),
  createdBy: 'Vũ Ngọc Mai (Giáo vụ)',
});

// Lớp đang học, sắp kết thúc
const n3 = MOCK_ENROLLMENTS.filter((e) => e.classId === 'C-N3K12' && e.status === 'studying').slice(0, 3);
const kids = MOCK_ENROLLMENTS.filter((e) => e.classId === 'C-KIDS05' && e.status === 'studying').slice(0, 2);
// Lớp đã kết thúc — đã giao Sale
// Ưu tiên học viên chưa học lớp nào khác (đúng đối tượng cần tư vấn lên lớp)
const n5 = MOCK_ENROLLMENTS.filter((e) => e.classId === 'C-N5K18' && e.status === 'completed')
  .sort((a, b) => Number(busyElsewhere(a.studentId, 'C-N5K18')) - Number(busyElsewhere(b.studentId, 'C-N5K18')))
  .slice(0, 4);

export const MOCK_PROMOTIONS: PromotionCandidate[] = [
  ...n3.map((e, i) => ({ ...base(e.studentId, 'C-N3K12', 'C-N2K06', i < 2, d(23)), source: i < 2 ? ('stats' as const) : ('manual' as const) })),
  ...kids.map((e) => base(e.studentId, 'C-KIDS05', 'C-KIDS06', true, d(17))),
  ...n5.map((e, i): PromotionCandidate => {
    const c = { ...base(e.studentId, 'C-N5K18', 'C-N4K17', i !== 3, d(9)), handedOverAt: ago(6), createdAt: ago(12), saleId: 'e-tung' };
    if (i === 0)
      return {
        ...c,
        status: 'consulting',
        interest: { at: ago(2, 20), by: 'student' },
        notes: [{ id: 'PN-1', at: ago(2, 15), by: 'Nguyễn Thanh Tùng', channel: 'call', text: 'Học viên muốn học tiếp nhưng hỏi lịch tối 2-4-6 có đổi sang 3-5 được không. Hẹn trả lời cuối tuần.' }],
      };
    if (i === 1) return { ...c, status: 'unreachable', notes: [{ id: 'PN-2', at: ago(1, 18), by: 'Nguyễn Thanh Tùng', channel: 'call', text: 'Gọi 2 lần không nghe máy — đã nhắn Zalo.' }] };
    if (i === 3) return { ...c, status: 'declined', declineReason: 'Bận lịch học / công việc', notes: [{ id: 'PN-3', at: ago(4, 11), by: 'Nguyễn Thanh Tùng', channel: 'zalo', text: 'Chuyển công tác vào Nam, tạm dừng học.' }] };
    return c;
  }),
];
