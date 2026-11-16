import dayjs from 'dayjs';
import { useCrmStore } from '@/features/crm/crmStore';
import { leaveRecord, usePortalStore } from '@/features/portal/portalStore';
import type { LeaveRequest } from '@/features/portal/types';
import { useStudentStore } from '@/features/student/studentStore';
import type { Enrollment } from '@/features/student/types';
import { hashString } from '@/lib/text';
import { useClassStore } from './classStore';
import { defaultRecords, rosterFor, summarize } from './sessionUtils';
import type { AttendanceRecord, ClassSession } from './types';

/**
 * Điểm danh bằng QR:
 *  - GV / giáo vụ mở mã QR của buổi (CỐ ĐỊNH theo buổi — giáo vụ còn xác nhận buổi học sau đó)
 *  - Học viên quét trên Cổng học viên (camera) hoặc nhập mã 6 ký tự in dưới QR
 *  - Quét sau giờ bắt đầu quá LATE_AFTER_MIN phút → "Đi muộn"
 */

/** Quét sớm nhất trước giờ học (phút) */
export const QR_OPEN_BEFORE_MIN = 30;
/** Quét sau giờ bắt đầu quá số phút này → tính "Đi muộn" */
export const QR_LATE_AFTER_MIN = 10;

const PREFIX = 'JPC-ATT';
const toMin = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));

/** Mã 6 ký tự của buổi (hiện dưới QR để HV nhập tay khi camera lỗi) */
export const qrCodeOf = (sessionId: string) => hashString(`${PREFIX}|${sessionId}`).toString(36).toUpperCase().padStart(6, '0').slice(-6);

/** Nội dung mã QR của buổi */
export const qrPayload = (s: Pick<ClassSession, 'id'>) => `${PREFIX}|${s.id}|${qrCodeOf(s.id)}`;

/** Đọc nội dung quét được / mã nhập tay */
export function parseScan(text: string): { sessionId?: string; code?: string } | null {
  const t = text.trim();
  if (t.startsWith(`${PREFIX}|`)) {
    const [, sessionId, code] = t.split('|');
    return sessionId && code === qrCodeOf(sessionId) ? { sessionId, code } : null;
  }
  return /^[A-Z0-9]{6}$/i.test(t) ? { code: t.toUpperCase() } : null;
}

/** Danh sách điểm danh hiện tại của buổi (chưa điểm danh → HV chưa quét = "Vắng"; có đơn nghỉ đã duyệt → "Có phép"; kèm HV lớp khác học bù) */
export function sessionRecords(s: ClassSession, roster: Enrollment[], leaves: LeaveRequest[]): AttendanceRecord[] {
  if (s.records) return s.records;
  const base = defaultRecords(roster).map((r) => {
    const lr = leaves.find((l) => l.sessionId === s.id && l.studentId === r.studentId && l.status === 'approved');
    return lr ? leaveRecord(lr) : r.status === 'excused' ? r : { ...r, status: 'absent' as const };
  });
  const guests: AttendanceRecord[] = leaves
    .filter((l) => l.status === 'approved' && l.makeupSessionId === s.id)
    .map((l) => ({ studentId: l.studentId, status: 'absent', makeupFor: { sessionId: l.sessionId, classId: l.classId, leaveRequestId: l.id } }));
  return [...base, ...guests];
}

/** Giờ "bây giờ" nếu đang trong ngày học, ngoài ra lấy giờ bắt đầu */
const nowOr = (s: ClassSession) => (dayjs().format('YYYY-MM-DD') === s.date ? dayjs().format('HH:mm') : s.startTime);

/** Ghi nhận 1 HV quét QR vào danh sách điểm danh */
export function applyQrScan(records: AttendanceRecord[], s: ClassSession, studentId: string): AttendanceRecord[] {
  const t = nowOr(s);
  const status = toMin(t) - toMin(s.startTime) > QR_LATE_AFTER_MIN ? ('late' as const) : ('present' as const);
  return records.map((r) => (r.studentId === studentId ? { ...r, status, checkInAt: t, method: 'qr' as const, note: undefined } : r));
}

export interface ScanResult {
  ok: boolean;
  title: string;
  description?: string;
  session?: ClassSession;
}

/** Cổng học viên: xử lý 1 lần quét / nhập mã → ghi điểm danh */
export function useQrCheckIn(studentId: string) {
  const sessions = useClassStore((s) => s.sessions);
  const classes = useClassStore((s) => s.classes);
  const updateSession = useClassStore((s) => s.updateSession);
  const enrollments = useStudentStore((s) => s.enrollments);
  const leaves = usePortalStore((s) => s.leaveRequests);
  const recordMakeup = usePortalStore((s) => s.recordMakeup);
  const studentName = useCrmStore((s) => s.students.find((x) => x.id === studentId)?.fullName);

  /** HV có thuộc buổi này không (HV của lớp hoặc HV lớp khác đăng ký học bù đã duyệt) */
  const belongs = (s: ClassSession) =>
    rosterFor(s.classId, s.date, enrollments).some((e) => e.studentId === studentId) ||
    leaves.some((l) => l.status === 'approved' && l.makeupSessionId === s.id && l.studentId === studentId);

  return (text: string): ScanResult => {
    const parsed = parseScan(text);
    if (!parsed) return { ok: false, title: 'Mã không hợp lệ', description: 'Đây không phải mã QR điểm danh của trung tâm. Hãy quét mã giáo viên đang chiếu trên lớp.' };

    const today = dayjs().format('YYYY-MM-DD');
    const s = parsed.sessionId
      ? sessions.find((x) => x.id === parsed.sessionId)
      : // Nhập tay: tìm buổi hôm nay của HV có mã khớp
        sessions.find((x) => x.date === today && qrCodeOf(x.id) === parsed.code && belongs(x));
    if (!s) return { ok: false, title: 'Không tìm thấy buổi học', description: parsed.sessionId ? 'Mã QR đã cũ hoặc buổi học không còn tồn tại.' : 'Mã không khớp buổi học nào của bạn hôm nay — kiểm tra lại 6 ký tự dưới mã QR.' };

    const cls = classes.find((c) => c.id === s.classId);
    const label = `${cls?.code.replace(/^GV[VN]*_/, '') ?? ''} · Buổi ${s.no || '(học thêm)'}`;
    if (s.status === 'cancelled') return { ok: false, title: 'Buổi học đã hủy', description: label, session: s };
    if (!belongs(s)) return { ok: false, title: 'Bạn không thuộc buổi học này', description: `${label} — nếu bạn đến học bù, nhờ giáo viên / giáo vụ điểm danh hộ.`, session: s };

    const start = dayjs(`${s.date}T${s.startTime}`);
    const end = dayjs(`${s.date}T${s.endTime}`);
    if (dayjs().isBefore(start.subtract(QR_OPEN_BEFORE_MIN, 'minute')))
      return { ok: false, title: 'Chưa tới giờ điểm danh', description: `${label} — mở điểm danh từ ${start.subtract(QR_OPEN_BEFORE_MIN, 'minute').format('HH:mm DD/MM')}.`, session: s };
    if (dayjs().isAfter(end)) return { ok: false, title: 'Buổi học đã kết thúc', description: `${label} — nếu bạn có đi học, nhờ giáo vụ điểm danh bổ sung.`, session: s };

    const roster = rosterFor(s.classId, s.date, enrollments);
    const records = sessionRecords(s, roster, leaves);
    const mine = records.find((r) => r.studentId === studentId);
    if (mine && (mine.status === 'present' || mine.status === 'late') && mine.checkInAt)
      return { ok: true, title: 'Bạn đã điểm danh rồi', description: `${label} — vào lớp lúc ${mine.checkInAt}${mine.status === 'late' ? ' (đi muộn)' : ''}.`, session: s };

    const next = applyQrScan(mine ? records : [...records, { studentId, status: 'absent' }], s, studentId);
    const sm = summarize(next);
    updateSession(s.id, {
      records: next,
      attendance: { present: sm.attended, total: sm.total },
      attendanceBy: s.attendanceBy ?? { name: 'Học viên quét QR', at: new Date().toISOString() },
    });
    const rec = next.find((r) => r.studentId === studentId)!;
    if (rec.makeupFor) recordMakeup(rec.makeupFor.leaveRequestId, rec.status);
    return {
      ok: true,
      title: rec.status === 'late' ? `Điểm danh thành công — đi muộn` : 'Điểm danh thành công',
      description: `${studentName ? `${studentName} · ` : ''}${label} · vào lớp lúc ${rec.checkInAt}`,
      session: s,
    };
  };
}
