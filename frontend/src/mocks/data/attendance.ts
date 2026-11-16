import dayjs from 'dayjs';
import { teacherName } from '@/features/academic/classes/constants';
import type { AttendanceRecord, AttendanceStatus, ClassSession, TeacherAttendance } from '@/features/academic/classes/types';
import { MOCK_CLASSES, MOCK_SESSIONS as RAW_SESSIONS } from './classes';
import { MOCK_ENROLLMENTS } from './students';

/**
 * Dữ liệu giả — điểm danh chi tiết từng học viên + chấm công giáo viên cho các buổi đã dạy.
 * Tách riêng vì cần cả buổi học (classes.ts) lẫn ghi danh (students.ts — vốn đã import classes.ts).
 * Tình huống: vắng không phép rải rác (có HV vắng 2 buổi liên tiếp), đi muộn, có phép, HV bảo lưu; GV đi muộn 1 buổi.
 */

const NOTES: Partial<Record<AttendanceStatus, string[]>> = {
  late: ['Kẹt xe, vào muộn 15 phút', 'Tan làm muộn'],
  excused: ['Xin nghỉ ốm (phụ huynh báo)', 'Đi công tác'],
};

const LEAVE = ['Đau bụng, xin về sớm', 'Phụ huynh đón sớm — việc gia đình', 'Mệt, sốt nhẹ'];

/** "18:00" ± phút → "HH:mm" */
function shift(t: string, min: number) {
  const [h, m] = t.split(':').map(Number);
  const total = h * 60 + m + min;
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`;
}

function recordsFor(s: ClassSession, i: number): AttendanceRecord[] | undefined {
  if (!s.attendance) return undefined;
  const roster = MOCK_ENROLLMENTS.filter((e) => e.classId === s.classId && (e.status !== 'dropped' && e.status !== 'transferred' ? true : (e.endedAt ?? '') > s.date));
  if (!roster.length) return undefined;
  // Quy đổi tỉ lệ vắng của số liệu tổng (sĩ số giả lập) sang sĩ số thật của lớp
  const absentCount = Math.min(roster.length - 1, Math.round(((s.attendance.total - s.attendance.present) / Math.max(1, s.attendance.total)) * roster.length));
  // Thứ tự buổi trong lớp (tính từ cuối) — để tạo 1 học viên "hay vắng" ở lớp đang học
  const done = RAW_SESSIONS.filter((x) => x.classId === s.classId && x.attendance);
  const fromEnd = done.length - 1 - done.findIndex((x) => x.id === s.id);
  const ongoing = MOCK_CLASSES.find((c) => c.id === s.classId)?.status === 'ongoing';
  const studying = roster.filter((e) => e.status === 'studying');
  const truant = ongoing && studying.length >= 2 ? studying[studying.length - 1].studentId : undefined;
  // HV "chuyên cần thấp": vắng rải rác 1/3 số buổi (không liên tiếp) → cảnh báo mức trung bình
  const slacker = ongoing && studying.length >= 3 ? studying[0].studentId : undefined;
  return roster.map((e, k): AttendanceRecord => {
    if (e.status === 'reserved') return { studentId: e.studentId, status: 'excused', note: 'Đang bảo lưu' };
    // HV "hay vắng": vắng 2 buổi gần nhất + rải rác → hiện ở "Học viên cần chú ý"
    if (e.studentId === truant && (fromEnd < 2 || fromEnd % 5 === 3)) return { studentId: e.studentId, status: 'absent' };
    if (e.studentId === slacker && fromEnd % 3 === 1) return { studentId: e.studentId, status: 'absent' };
    // HV đầu danh sách vắng xen kẽ → có lúc vắng 2 buổi liền (để demo cảnh báo)
    const slot = (k + i) % roster.length;
    if (slot < absentCount) {
      const status: AttendanceStatus = (k + i) % 3 === 0 ? 'excused' : 'absent';
      return { studentId: e.studentId, status, note: status === 'excused' ? NOTES.excused![k % 2] : undefined };
    }
    if ((k * 7 + i) % 11 === 0) return { studentId: e.studentId, status: 'late', note: NOTES.late![i % 2], checkInAt: shift(s.startTime, 12 + (k % 3) * 5), method: 'qr' };
    // Có mặt: phần lớn tự quét QR trước giờ học; vài HV quên điện thoại → GV điểm danh
    const rec: AttendanceRecord = { studentId: e.studentId, status: 'present', checkInAt: shift(s.startTime, -((k * 3 + i) % 12)), method: (k + i) % 9 === 4 ? 'teacher' : 'qr' };
    // Về sớm giữa buổi
    if (k === 1 && i % 6 === 2) return { ...rec, leftAt: shift(s.endTime, -45), leaveReason: LEAVE[i % LEAVE.length] };
    return rec;
  });
}

const STAFF = ['Phạm Thị Hồng (Giáo vụ)', 'Lý Thanh Vy (Giáo vụ)', 'Trần Minh Khoa (Giáo vụ)'];
const today = dayjs().format('YYYY-MM-DD');
const at = (date: string, hhmm: string, plusMin = 0) => dayjs(`${date}T${hhmm}`).add(plusMin, 'minute').toISOString();

/** GV + trợ giảng TỰ chấm công trên Cổng giáo viên; 1 buổi / 13 đi muộn; buổi gần nhất có GV quên check-out */
function teacherAttendanceFor(s: ClassSession, i: number, recent: boolean): TeacherAttendance[] | undefined {
  if (!s.attendance) return undefined;
  const cls = MOCK_CLASSES.find((c) => c.id === s.classId);
  if (!cls) return undefined;
  const teachers = (s.teacherIds ?? cls.teacherIds).map((teacherId): TeacherAttendance => {
    const late = i % 13 === 6;
    const checkIn = late ? s.startTime.replace(/:00$/, ':10') : s.startTime.replace(/:00$/, ':00');
    const forgotOut = recent && i % 3 === 0;
    return {
      teacherId,
      role: s.teacherIds ? 'substitute' : 'teacher',
      status: late ? 'late' : 'present',
      checkIn,
      checkOut: forgotOut ? undefined : s.endTime,
      note: late ? 'Kẹt xe' : forgotOut ? 'Quên check-out' : undefined,
      source: 'self',
      checkedAt: at(s.date, forgotOut ? checkIn : s.endTime, 2),
    };
  });
  const assistants = (s.assistantIds ?? cls.assistantIds ?? []).map(
    (teacherId): TeacherAttendance => ({ teacherId, role: 'assistant', status: 'present', checkIn: s.startTime, checkOut: s.endTime, source: 'self', checkedAt: at(s.date, s.endTime, 5) }),
  );
  return [...teachers, ...assistants];
}

const BASE_SESSIONS: ClassSession[] = RAW_SESSIONS.map((s, i) => {
  const records = recordsFor(s, i);
  if (!records) return s;
  // Buổi trong 2 ngày gần nhất: nhân viên chưa xác nhận
  const recent = dayjs(today).diff(dayjs(s.date), 'day') <= 2;
  const attended = records.filter((r) => r.status === 'present' || r.status === 'late').length;
  const cls = MOCK_CLASSES.find((c) => c.id === s.classId);
  return {
    ...s,
    origin: s.status === 'makeup' ? 'makeup' : undefined,
    makeupForId: s.status === 'makeup' ? RAW_SESSIONS.find((x) => x.classId === s.classId && x.status === 'cancelled' && s.note === `Học bù cho buổi ${x.no}`)?.id : undefined,
    records,
    attendance: { present: attended, total: records.length },
    attendanceBy: cls ? { name: teacherName(cls.teacherIds[0]), at: `${s.date}T${s.startTime}` } : undefined,
    teacherAttendance: teacherAttendanceFor(s, i, recent),
    confirmation: recent ? undefined : { by: STAFF[i % STAFF.length], at: at(s.date, s.endTime, 40 + (i % 50)) },
  };
});

/**
 * 2 tình huống SỰ CỐ giáo viên giữa buổi (offline):
 *  - Buổi đã dạy gần nhất của N4-K15: GV Long sốt cao lúc 19:00 → GV Tanaka vào dạy thay nửa sau
 *  - Buổi thiếu nhi hôm nay / gần nhất: GV Mai Anh choáng lúc 10:00 → trợ giảng Linh dạy tiếp
 */
function withIncidents(list: ClassSession[]): ClassSession[] {
  const lastDone = (classId: string) => [...list].reverse().find((x) => x.classId === classId && x.attendance && x.status === 'completed');
  const n4 = lastDone('C-N4K15');
  const kids = list.find((x) => x.classId === 'C-KIDS05' && x.date === today) ?? lastDone('C-KIDS05');
  return list.map((x) => {
    if (n4 && x.id === n4.id) {
      return {
        ...x,
        confirmation: undefined,
        supportLog: [
          { id: 'SL-n4-1', kind: 'incident', note: 'GV Long sốt cao — gọi GV Tanaka (đang trực văn phòng) vào dạy thay từ 19:10.', by: 'Phạm Thị Hồng (Giáo vụ)', at: at(x.date, '19:03') },
          { id: 'SL-n4-2', kind: 'teacher_checkin', note: 'Chấm công hộ GV Tanaka (chưa có tài khoản Cổng GV ở chi nhánh này).', by: 'Phạm Thị Hồng (Giáo vụ)', at: at(x.date, '19:12') },
        ],
        incident: { at: '19:00', reason: 'sick', detail: 'GV sốt cao, chóng mặt giữa giờ.', action: 'substitute', takeoverId: 't-tanaka', notified: true, reportedBy: 'Phạm Thị Hồng (Giáo vụ)', reportedAt: at(x.date, '19:03') },
        teacherAttendance: [
          { teacherId: 't-long', role: 'teacher', status: 'present', checkIn: x.startTime, checkOut: '19:00', note: 'Sốt cao — về sớm', source: 'self', checkedAt: at(x.date, x.startTime, 1) },
          { teacherId: 't-tanaka', role: 'substitute', status: 'present', checkIn: '19:10', checkOut: x.endTime, note: 'Dạy thay nửa sau buổi', source: 'staff', checkedAt: at(x.date, '19:10') },
        ],
      };
    }
    if (kids && x.id === kids.id) {
      return {
        ...x,
        confirmation: undefined,
        supportLog: [
          { id: 'SL-k-1', kind: 'incident', note: 'GV Mai Anh bị choáng — đưa về phòng GV nghỉ, trợ giảng Linh đứng lớp.', by: 'Lễ tân Cầu Giấy', at: at(x.date, '10:02') },
          { id: 'SL-k-2', kind: 'attendance', note: '2 bé chưa có điện thoại quét QR — lễ tân điểm danh hộ lúc 09:05.', by: 'Lễ tân Cầu Giấy', at: at(x.date, '09:05') },
        ],
        incident: { at: '10:00', reason: 'sick', detail: 'GV bị choáng, đã nghỉ ở phòng giáo viên.', action: 'assistant', takeoverId: 'ta-linh', notified: false, reportedBy: 'Lễ tân Cầu Giấy', reportedAt: at(x.date, '10:02') },
        teacherAttendance: [
          { teacherId: 't-anh', role: 'teacher', status: 'present', checkIn: '08:55', checkOut: '10:00', note: 'Choáng — nghỉ giữa buổi', source: 'self', checkedAt: at(x.date, '08:55') },
          { teacherId: 'ta-linh', role: 'assistant', status: 'present', checkIn: '08:50', checkOut: x.date === today ? undefined : x.endTime, source: 'self', checkedAt: at(x.date, '08:50') },
        ],
      };
    }
    return x;
  });
}

export const MOCK_SESSIONS: ClassSession[] = withIncidents(BASE_SESSIONS);
