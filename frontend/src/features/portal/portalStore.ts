import dayjs from 'dayjs';
import { create } from 'zustand';
import { MOCK_CANCEL_REQUESTS, MOCK_CARD_PROGRESS, MOCK_DECKS, MOCK_LEAVE_REQUESTS, MOCK_STUDY_DAYS, MOCK_SUB_INVITES, MOCK_TEACHER_DOCS } from '@/mocks/data/portal';
import { recordActivity } from '@/features/system/activity';
import { useClassStore } from '@/features/academic/classes/classStore';
import type { AttendanceRecord } from '@/features/academic/classes/types';
import type { CancelRequest, CardProgress, FlashDeck, LeaveRequest, SubInvite, TeacherDoc } from './types';
import { moveToTrash, registerRestorer } from '@/features/system/trash';

/** Store CỔNG giả lập. Khi có backend: thay hàm bằng gọi API, giữ nguyên chữ ký. */

/** Khoảng ôn lại (ngày) theo hộp Leitner */
export const BOX_INTERVAL = [0, 1, 3, 7, 14, 30];

interface PortalState {
  cancelRequests: CancelRequest[];
  subInvites: SubInvite[];
  teacherDocs: TeacherDoc[];
  leaveRequests: LeaveRequest[];
  decks: FlashDeck[];
  cardProgress: Record<string, CardProgress>;
  studyDays: string[];
  /** Phụ huynh đang xem con nào */
  childId?: string;

  addCancelRequest: (r: Omit<CancelRequest, 'id' | 'createdAt' | 'status'>) => void;
  withdrawCancelRequest: (id: string) => void;
  respondInvite: (id: string, accept: boolean, declineReason?: string) => void;
  /** Giáo vụ mời 1 hoặc nhiều GV dạy thay 1 buổi (ai nhận trước thì dạy) */
  addSubInvites: (list: Omit<SubInvite, 'id' | 'createdAt' | 'status'>[]) => void;
  /** Giáo vụ thu hồi lời mời chưa trả lời */
  withdrawInvite: (id: string) => void;
  /** Giáo vụ duyệt / từ chối đơn (Bảng điều khiển nhân viên) */
  decideCancelRequest: (id: string, approve: boolean, by: string, note?: string) => void;
  decideLeaveRequest: (id: string, approve: boolean, by: string, note?: string) => void;
  /** GV lớp học bù điểm danh HV học bù → ghi kết quả vào đơn + buổi gốc (nếu đã điểm danh) */
  recordMakeup: (leaveRequestId: string, status: NonNullable<LeaveRequest['makeupResult']>) => void;

  addTeacherDoc: (d: Omit<TeacherDoc, 'id' | 'createdAt' | 'viewedBy'>) => void;
  updateTeacherDoc: (id: string, patch: Partial<TeacherDoc>) => void;
  removeTeacherDoc: (id: string) => void;
  viewTeacherDoc: (id: string, studentId: string) => void;

  addLeaveRequest: (r: Omit<LeaveRequest, 'id' | 'createdAt' | 'status'>) => void;
  withdrawLeaveRequest: (id: string) => void;
  setMakeup: (id: string, makeupSessionId?: string) => void;

  /** Chấm 1 thẻ: nhớ → lên hộp, quên → về hộp 0 */
  reviewCard: (cardId: string, remembered: boolean) => void;
  setChild: (id: string) => void;
}

let seq = 100;
const now = () => new Date().toISOString();

export const usePortalStore = create<PortalState>((set, get) => ({
  cancelRequests: MOCK_CANCEL_REQUESTS,
  subInvites: MOCK_SUB_INVITES,
  teacherDocs: MOCK_TEACHER_DOCS,
  leaveRequests: MOCK_LEAVE_REQUESTS,
  decks: MOCK_DECKS,
  cardProgress: MOCK_CARD_PROGRESS,
  studyDays: MOCK_STUDY_DAYS,

  addCancelRequest: (r) => {
    const request = { ...r, id: `CR-${++seq}`, createdAt: now(), status: 'pending' as const };
    set((s) => ({ cancelRequests: [request, ...s.cancelRequests] }));
    recordActivity({
      category: 'data',
      severity: 'warning',
      action: 'Gửi đơn hủy buổi',
      target: request.classId,
      detail: request.reason,
      notifications: [{ portal: 'staff', title: 'Đơn hủy buổi mới', description: `Giáo viên gửi yêu cầu cho lớp ${request.classId}.`, tone: 'warning' }],
    });
  },
  withdrawCancelRequest: (id) => {
    const request = get().cancelRequests.find((item) => item.id === id);
    if (!request) return;
    set((s) => ({ cancelRequests: s.cancelRequests.filter((x) => x.id !== id) }));
    recordActivity({ category: 'data', severity: 'info', action: 'Thu hồi đơn hủy buổi', target: id });
  },
  respondInvite: (id, accept, declineReason) => {
    const invite = get().subInvites.find((item) => item.id === id);
    if (!invite || invite.status !== 'pending') return;
    set((s) => {
      const inv = s.subInvites.find((x) => x.id === id);
      return {
        subInvites: s.subInvites.map((x) => {
          if (x.id === id) return { ...x, status: accept ? 'accepted' : 'declined', respondedAt: now(), declineReason };
          // Đã có người nhận → các lời mời khác của buổi này hết hiệu lực
          if (accept && inv && x.sessionId === inv.sessionId && x.status === 'pending') return { ...x, status: 'expired', respondedAt: now(), declineReason: 'GV khác đã nhận' };
          return x;
        }),
      };
    });
    recordActivity({
      category: 'data',
      severity: accept ? 'info' : 'warning',
      action: accept ? 'Nhận lời dạy thay' : 'Từ chối lời mời dạy thay',
      target: invite.classId,
      detail: declineReason,
      notifications: [{ portal: 'staff', title: accept ? 'Giáo viên đã nhận lời' : 'Giáo viên từ chối lời mời', description: `Lớp ${invite.classId}.`, tone: accept ? 'success' : 'warning' }],
    });
  },
  addSubInvites: (list) => {
    if (!list.length) return;
    const invites = list.map((x) => ({ ...x, id: `SI-${++seq}`, createdAt: now(), status: 'pending' as const }));
    set((s) => ({ subInvites: [...invites, ...s.subInvites] }));
    recordActivity({
      category: 'data',
      severity: 'info',
      action: 'Gửi lời mời dạy thay',
      target: invites.map((invite) => invite.classId).join(', '),
      notifications: invites.map((invite) => ({
        portal: 'teacher' as const,
        title: 'Lời mời dạy thay mới',
        description: `Bạn được mời dạy lớp ${invite.classId}.`,
        tone: 'info' as const,
      })),
    });
  },
  withdrawInvite: (id) => {
    const invite = get().subInvites.find((item) => item.id === id);
    if (!invite || invite.status !== 'pending') return;
    set((s) => ({ subInvites: s.subInvites.map((x) => (x.id === id ? { ...x, status: 'expired', respondedAt: now(), declineReason: 'Giáo vụ thu hồi' } : x)) }));
    recordActivity({ category: 'data', severity: 'warning', action: 'Thu hồi lời mời dạy thay', target: invite.classId });
  },

  decideCancelRequest: (id, approve, by, note) => {
    const request = get().cancelRequests.find((item) => item.id === id);
    if (!request || request.status !== 'pending') return;
    set((s) => ({ cancelRequests: s.cancelRequests.map((x) => (x.id === id ? { ...x, status: approve ? 'approved' : 'rejected', decidedBy: by, decidedAt: now(), note } : x)) }));
    recordActivity({
      category: 'data',
      severity: approve ? 'info' : 'warning',
      action: approve ? 'Duyệt đơn hủy buổi' : 'Từ chối đơn hủy buổi',
      target: request.classId,
      detail: note,
      actor: { name: by, role: 'Nhân viên' },
      notifications: [{ portal: 'teacher', title: approve ? 'Đơn hủy buổi đã được duyệt' : 'Đơn hủy buổi bị từ chối', description: `Yêu cầu lớp ${request.classId}${note ? `: ${note}` : '.'}`, tone: approve ? 'success' : 'warning' }],
    });
  },
  decideLeaveRequest: (id, approve, by, note) => {
    const request = get().leaveRequests.find((item) => item.id === id);
    if (!request || request.status !== 'pending') return;
    set((s) => ({ leaveRequests: s.leaveRequests.map((x) => (x.id === id ? { ...x, status: approve ? 'approved' : 'rejected', decidedBy: by, decidedAt: now(), note } : x)) }));
    const lr = get().leaveRequests.find((x) => x.id === id);
    if (lr) syncLeaveToSession(lr);
    const portal = request.by === 'parent' ? 'parent' : 'student';
    recordActivity({
      category: 'data',
      severity: approve ? 'info' : 'warning',
      action: approve ? 'Duyệt đơn xin nghỉ' : 'Từ chối đơn xin nghỉ',
      target: request.studentId,
      detail: note,
      actor: { name: by, role: 'Nhân viên' },
      notifications: [{ portal, title: approve ? 'Đơn xin nghỉ đã được duyệt' : 'Đơn xin nghỉ bị từ chối', description: `Yêu cầu nghỉ lớp ${request.classId}${note ? `: ${note}` : '.'}`, tone: approve ? 'success' : 'warning' }],
    });
  },
  recordMakeup: (leaveRequestId, status) => {
    set((s) => ({ leaveRequests: s.leaveRequests.map((x) => (x.id === leaveRequestId ? { ...x, makeupResult: status } : x)) }));
    const lr = get().leaveRequests.find((x) => x.id === leaveRequestId);
    if (lr) syncLeaveToSession(lr);
  },

  addTeacherDoc: (d) => set((s) => ({ teacherDocs: [{ ...d, id: `TD-${++seq}`, createdAt: now(), viewedBy: [] }, ...s.teacherDocs] })),
  updateTeacherDoc: (id, patch) => set((s) => ({ teacherDocs: s.teacherDocs.map((x) => (x.id === id ? { ...x, ...patch } : x)) })),
  removeTeacherDoc: (id) => {
    const d = usePortalStore.getState().teacherDocs.find((x) => x.id === id);
    if (d) moveToTrash({ kind: 'teacher_doc', label: d.title, detail: d.fileName, payload: d });
    set((s) => ({ teacherDocs: s.teacherDocs.filter((x) => x.id !== id) }));
  },
  viewTeacherDoc: (id, studentId) =>
    set((s) => ({ teacherDocs: s.teacherDocs.map((x) => (x.id === id && !x.viewedBy.includes(studentId) ? { ...x, viewedBy: [...x.viewedBy, studentId] } : x)) })),

  addLeaveRequest: (r) => {
    const request = { ...r, id: `LR-${++seq}`, createdAt: now(), status: 'pending' as const };
    set((s) => ({ leaveRequests: [request, ...s.leaveRequests] }));
    recordActivity({
      category: 'data',
      severity: 'info',
      action: 'Gửi đơn xin nghỉ',
      target: request.studentId,
      detail: `Lớp ${request.classId} · ${request.reason}`,
      notifications: [{ portal: 'staff', title: 'Đơn xin nghỉ mới', description: `${request.byName} gửi đơn xin nghỉ lớp ${request.classId}.`, tone: 'info' }],
    });
  },
  withdrawLeaveRequest: (id) => {
    const request = get().leaveRequests.find((item) => item.id === id);
    if (!request) return;
    set((s) => ({ leaveRequests: s.leaveRequests.filter((x) => x.id !== id) }));
    recordActivity({ category: 'data', severity: 'info', action: 'Thu hồi đơn xin nghỉ', target: request.studentId });
  },
  setMakeup: (id, makeupSessionId) => set((s) => ({ leaveRequests: s.leaveRequests.map((x) => (x.id === id ? { ...x, makeupSessionId } : x)) })),

  reviewCard: (cardId, remembered) =>
    set((s) => {
      const cur = s.cardProgress[cardId] ?? { box: 0, due: now(), reviews: 0, lapses: 0 };
      const box = remembered ? Math.min(5, cur.box + 1) : 0;
      const today = dayjs().format('YYYY-MM-DD');
      return {
        cardProgress: {
          ...s.cardProgress,
          [cardId]: { box, due: dayjs().add(BOX_INTERVAL[box], 'day').startOf('day').toISOString(), reviews: cur.reviews + 1, lapses: cur.lapses + (remembered ? 0 : 1) },
        },
        studyDays: s.studyDays.includes(today) ? s.studyDays : [today, ...s.studyDays],
      };
    }),
  setChild: (id) => set({ childId: id }),
}));

/** Chuỗi ngày học liên tiếp (tính tới hôm nay hoặc hôm qua) */
export function streakOf(days: string[]): number {
  const set = new Set(days);
  let d = set.has(dayjs().format('YYYY-MM-DD')) ? dayjs() : dayjs().subtract(1, 'day');
  let n = 0;
  while (set.has(d.format('YYYY-MM-DD'))) {
    n++;
    d = d.subtract(1, 'day');
  }
  return n;
}

// ---------------- Đơn xin nghỉ ↔ điểm danh ----------------

/** Học bù có mặt → thông tin "đã học bù" gắn vào buổi gốc */
export function madeUpOf(lr: LeaveRequest): AttendanceRecord['madeUp'] {
  if (!lr.makeupSessionId || (lr.makeupResult !== 'present' && lr.makeupResult !== 'late')) return undefined;
  const { sessions, classes } = useClassStore.getState();
  const ms = sessions.find((x) => x.id === lr.makeupSessionId);
  const mc = classes.find((c) => c.id === ms?.classId);
  return { sessionId: lr.makeupSessionId, classCode: mc?.code.replace(/^GV[VN]*_/, '') ?? '', status: lr.makeupResult };
}

/** Bản ghi điểm danh mặc định cho HV có đơn nghỉ ĐÃ DUYỆT */
export function leaveRecord(lr: LeaveRequest): AttendanceRecord {
  return { studentId: lr.studentId, status: 'excused', note: `Có đơn xin nghỉ — ${lr.reasonType}`, leaveRequestId: lr.id, madeUp: madeUpOf(lr) };
}

/**
 * Buổi gốc ĐÃ điểm danh mà đơn mới được duyệt / mới học bù xong → sửa lại bản ghi:
 *  - đang "vắng" (không phép) → "có phép" theo đơn
 *  - gắn / gỡ "đã học bù" theo kết quả buổi học bù
 *  HV vẫn đến học (có mặt / muộn) thì giữ nguyên.
 */
function syncLeaveToSession(lr: LeaveRequest) {
  const { sessions, updateSession } = useClassStore.getState();
  const s = sessions.find((x) => x.id === lr.sessionId);
  if (!s?.records || lr.status !== 'approved') return;
  const records = s.records.map((r) => {
    if (r.studentId !== lr.studentId || r.status === 'present' || r.status === 'late') return r;
    return { ...r, status: 'excused' as const, note: r.note ?? `Có đơn xin nghỉ — ${lr.reasonType}`, leaveRequestId: lr.id, madeUp: madeUpOf(lr) };
  });
  updateSession(s.id, { records });
}

registerRestorer<TeacherDoc>('teacher_doc', (d) => usePortalStore.setState((s) => ({ teacherDocs: [d, ...s.teacherDocs] })));
