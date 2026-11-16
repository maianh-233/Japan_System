import { useEffect } from 'react';
import dayjs from 'dayjs';
import { create } from 'zustand';
import { useClassStore } from '@/features/academic/classes/classStore';
import type { AttendanceRecord, ClassInfo, ClassSession } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { useCrmStore } from '@/features/crm/crmStore';
import { fillTemplate, firstName, missingVars, useMessageStore, type MessageChannel, type MessageScenario } from '@/features/crm/messages/messages';
import type { Lead, Student } from '@/features/crm/types';
import { useNotificationStore } from '@/features/notifications/notificationStore';
import { useStudentStore } from '@/features/student/studentStore';
import { getSettings } from '@/features/system/settings';
import { useSystemStore } from '@/features/system/systemStore';
import { usePortalStore } from './portalStore';
import { buildWeeklyReport, reportLink, reportSummary, weekLabel, weekStartOf } from './weeklyReport';

/**
 * THÔNG BÁO TỰ ĐỘNG CHO PHỤ HUYNH (giả lập job phía server):
 *  1. Con ĐÃ ĐẾN LỚP — ngay khi buổi học được điểm danh có mặt / đi muộn (GV, nhân viên hoặc HV quét QR)
 *  2. Con CHƯA ĐẾN LỚP — quá N phút sau giờ vào lớp mà chưa có mặt và không có đơn xin nghỉ
 *     (GV điểm danh "vắng" trước mốc đó cũng báo luôn)
 *  3. BÁO CÁO TUẦN — gửi vào ngày + giờ cấu hình (mặc định CN 20:00)
 * Người nhận: người thân liên kết với học viên (không phải chính học viên) có bật "Nhận thông báo điểm danh / kết quả".
 * Kênh: Zalo nếu phụ huynh đăng ký Zalo, không thì SMS. Nội dung theo Mẫu tin nhắn (trung tâm sửa được).
 * Mỗi sự kiện chỉ gửi 1 lần (khóa: buổi × học viên × loại).
 */

type AlertKind = 'arrival' | 'absent' | 'weekly';

interface AlertState {
  /** khóa đã gửi → thời điểm */
  sent: Record<string, string>;
  mark: (key: string) => void;
}

export const useParentAlertStore = create<AlertState>((set) => ({
  sent: {},
  mark: (key) => set((s) => ({ sent: { ...s.sent, [key]: new Date().toISOString() } })),
}));

const keyOf = (kind: AlertKind, a: string, b: string) => `${kind}:${a}:${b}`;
const shortCode = (cls: ClassInfo) => cls.code.replace(/^GV[VN]*_/, '');

/** Phụ huynh nhận thông báo của học viên */
export function guardiansOf(studentId: string): Lead[] {
  const { links, leads } = useCrmStore.getState();
  return links
    .filter((k) => k.studentId === studentId && k.relationship !== 'self' && k.notify !== false)
    .map((k) => leads.find((l) => l.id === k.leadId))
    .filter((l): l is Lead => !!l && !l.hidden);
}

const channelOf = (lead: Lead): MessageChannel => (!lead.notifyChannels?.length || lead.notifyChannels.includes('zalo') ? 'zalo' : 'sms');

/** Học viên có thuộc diện báo đến lớp không (Cài đặt: chỉ dưới N tuổi) */
function inAgeScope(st: Student): boolean {
  const { underAge } = getSettings().parent;
  if (!underAge) return true;
  return !st.dob || dayjs().diff(dayjs(st.dob), 'year') < underAge;
}

/** Gửi 1 tin theo mẫu tới mọi phụ huynh của học viên. Trả về số người nhận. */
function deliver(scenario: MessageScenario, st: Student, vars: Record<string, string | undefined>, notice: { title: string; description: string; tone: 'info' | 'success' | 'warning' | 'danger'; link: string }): number {
  const tpl = useMessageStore.getState().templates.find((t) => t.active && t.scenario === scenario);
  const branch = useSystemStore.getState().branches.find((b) => b.id === st.branchId);
  const parents = guardiansOf(st.id);
  let n = 0;
  parents.forEach((p) => {
    if (tpl) {
      const body = fillTemplate(tpl.body, { ten: firstName(p.fullName), ho_ten: p.fullName, hoc_vien: st.fullName, chi_nhanh: branch?.name, dia_chi: branch?.address, hotline: branch?.phone, ...vars });
      if (!missingVars(body).length) {
        useMessageStore.getState().send({ channel: channelOf(p), scenario, templateId: tpl.id, toName: p.fullName, toPhone: p.phone, leadId: p.id, studentId: st.id, body, sentBy: 'Hệ thống', auto: true });
        n++;
      }
    }
  });
  // Thông báo trong Cổng phụ huynh (kể cả khi tắt mẫu tin)
  if (parents.length) useNotificationStore.getState().add('parent', notice);
  return n;
}

// ============================== 1 + 2. Điểm danh ==============================

function onAttendance(session: ClassSession, cls: ClassInfo, rec: AttendanceRecord) {
  const cfg = getSettings().parent;
  const st = useCrmStore.getState().students.find((s) => s.id === rec.studentId);
  if (!st || !inAgeScope(st) || session.date !== dayjs().format('YYYY-MM-DD')) return;
  const store = useParentAlertStore.getState();
  const gio = `${session.startTime}–${session.endTime}`;

  if ((rec.status === 'present' || rec.status === 'late') && cfg.arrivalNotice) {
    const k = keyOf('arrival', session.id, st.id);
    if (store.sent[k]) return;
    store.mark(k);
    const at = rec.checkInAt ?? dayjs().format('HH:mm');
    const tinhTrang = rec.status === 'late' ? 'đi muộn' : 'đúng giờ';
    deliver('arrival', st, { lop: shortCode(cls), gio, gio_vao: at, tinh_trang: tinhTrang }, {
      title: `${firstName(st.fullName)} đã đến lớp ${shortCode(cls)} lúc ${at}`,
      description: `${rec.status === 'late' ? 'Đi muộn' : 'Đúng giờ'} · buổi ${gio}${rec.method === 'qr' ? ' · tự quét QR' : ''}`,
      tone: rec.status === 'late' ? 'warning' : 'success',
      link: '/parent/attendance',
    });
  }
  // GV điểm danh vắng (không phép) → báo luôn, không chờ mốc N phút
  if (rec.status === 'absent' && !rec.leaveRequestId && cfg.absentAlertMin > 0) sendAbsent(session, cls, st);
}

function sendAbsent(session: ClassSession, cls: ClassInfo, st: Student) {
  const store = useParentAlertStore.getState();
  const k = keyOf('absent', session.id, st.id);
  if (store.sent[k] || store.sent[keyOf('arrival', session.id, st.id)]) return;
  store.mark(k);
  deliver('absent_alert', st, { lop: shortCode(cls), gio: session.startTime }, {
    title: `Chưa thấy ${firstName(st.fullName)} đến lớp ${shortCode(cls)}`,
    description: `Lớp bắt đầu ${session.startTime}. Nếu con nghỉ, vui lòng gửi đơn xin nghỉ hoặc gọi trung tâm.`,
    tone: 'danger',
    link: '/parent/attendance',
  });
}

/** Theo dõi thay đổi điểm danh trong store lớp học → báo đến lớp ngay khi có bản ghi mới */
function watchAttendance() {
  return useClassStore.subscribe((state, prev) => {
    if (state.sessions === prev.sessions) return;
    const before = new Map(prev.sessions.map((s) => [s.id, s]));
    state.sessions.forEach((s) => {
      const old = before.get(s.id);
      if (old === s || !s.records?.length || s.records === old?.records) return;
      const cls = state.classes.find((c) => c.id === s.classId);
      if (!cls) return;
      s.records.forEach((r) => {
        const o = old?.records?.find((x) => x.studentId === r.studentId);
        if (o?.status !== r.status) onAttendance(s, cls, r);
      });
    });
  });
}

/** Quét các buổi đang diễn ra: quá N phút chưa có mặt, không có đơn nghỉ → báo phụ huynh */
export function runAbsentCheck(now = dayjs()): number {
  const { absentAlertMin } = getSettings().parent;
  if (absentAlertMin <= 0) return 0;
  const today = now.format('YYYY-MM-DD');
  const { sessions, classes } = useClassStore.getState();
  const { enrollments } = useStudentStore.getState();
  const { students } = useCrmStore.getState();
  const leaves = usePortalStore.getState().leaveRequests;
  let n = 0;
  sessions
    .filter((s) => s.date === today && s.status !== 'cancelled')
    .forEach((s) => {
      const start = dayjs(`${s.date}T${s.startTime}`);
      if (now.isBefore(start.add(absentAlertMin, 'minute')) || !now.isBefore(dayjs(`${s.date}T${s.endTime}`))) return;
      const cls = classes.find((c) => c.id === s.classId);
      if (!cls) return;
      enrollments
        .filter((e) => e.classId === cls.id && e.status === 'studying' && dayjs(e.enrolledAt).isBefore(start))
        .forEach((e) => {
          const rec = s.records?.find((r) => r.studentId === e.studentId);
          if (rec && rec.status !== 'absent') return; // có mặt / muộn / có phép
          if (rec?.leaveRequestId || leaves.some((l) => l.sessionId === s.id && l.studentId === e.studentId && (l.status === 'approved' || l.status === 'pending'))) return;
          const st = students.find((x) => x.id === e.studentId);
          if (!st || !inAgeScope(st) || !guardiansOf(st.id).length) return;
          const before = Object.keys(useParentAlertStore.getState().sent).length;
          sendAbsent(s, cls, st);
          if (Object.keys(useParentAlertStore.getState().sent).length > before) n++;
        });
    });
  return n;
}

// ============================== 3. Báo cáo tuần ==============================

/** Gửi báo cáo tuần cho mọi học viên đang học có phụ huynh. `force` = gửi ngay (nút "Gửi thử") */
export function runWeeklyReports(now = dayjs(), force = false): number {
  const cfg = getSettings().parent;
  if (!cfg.weeklyReport && !force) return 0;
  let week = weekStartOf(now);
  // Mốc gửi trong tuần hiện tại (CN = ngày cuối tuần)
  const [h, m] = cfg.weeklyTime.split(':').map(Number);
  const sendAt = week.add((cfg.weeklyDay + 6) % 7, 'day').hour(h).minute(m);
  if (now.isBefore(sendAt)) {
    if (!force) return 0;
    week = week.subtract(7, 'day'); // gửi thử khi chưa tới giờ → báo cáo tuần trước (đủ 7 ngày)
  }

  const { enrollments, invoices } = useStudentStore.getState();
  const { classes, sessions, assignments, submissions, examResults } = useClassStore.getState();
  const curricula = useCurriculumStore.getState().curricula;
  const { students } = useCrmStore.getState();
  const store = useParentAlertStore.getState();
  const ids = [...new Set(enrollments.filter((e) => e.status === 'studying' || e.status === 'reserved').map((e) => e.studentId))];
  let n = 0;
  ids.forEach((id) => {
    const st = students.find((s) => s.id === id);
    const k = keyOf('weekly', week.format('YYYY-MM-DD'), id);
    if (!st || st.hidden || store.sent[k] || !guardiansOf(id).length) return;
    const r = buildWeeklyReport(id, week, { enrollments, invoices, classes, sessions, assignments, submissions, examResults, curricula }, now);
    if (!r.classes.length) return;
    store.mark(k);
    deliver('weekly_report', st, { lop: r.classes.map(shortCode).join(', '), tuan: weekLabel(week), tom_tat: reportSummary(r), link: reportLink(id, week) }, {
      title: `Báo cáo tuần ${weekLabel(week)} của ${firstName(st.fullName)}`,
      description: reportSummary(r),
      tone: 'info',
      link: `/parent/weekly?week=${week.format('YYYY-MM-DD')}`,
    });
    n++;
  });
  return n;
}

/** Chạy các job thông báo phụ huynh (gắn 1 lần ở gốc ứng dụng) */
export function useParentAlerts(intervalMs = 60_000) {
  useEffect(() => {
    const unsub = watchAttendance();
    const tick = () => {
      runAbsentCheck();
      runWeeklyReports();
    };
    tick();
    const t = window.setInterval(tick, intervalMs);
    return () => {
      unsub();
      window.clearInterval(t);
    };
  }, [intervalMs]);
}
