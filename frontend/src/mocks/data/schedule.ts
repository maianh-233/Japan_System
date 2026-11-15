import dayjs, { type Dayjs } from 'dayjs';
import type { TimetableEvent } from '@/components/common';

export interface MockSession extends TimetableEvent {
  className: string;
  teacher: string;
  room: string;
  online?: boolean;
  meetingUrl?: string;
  studentCount: number;
}

/** Thứ 2 của tuần chứa `d` */
export const weekStartOf = (d: Dayjs) => d.subtract((d.day() + 6) % 7, 'day').startOf('day');

/**
 * Dữ liệu giả — các buổi học lặp lại hằng tuần.
 * day: 0 = Thứ 2 ... 6 = Chủ nhật
 */
const TEMPLATE = [
  { day: 0, from: '18:00', to: '19:30', className: 'N3-K12', teacher: 'Nguyễn Thu Hà', room: 'P.301', tone: 'primary', students: 18 },
  { day: 2, from: '18:00', to: '19:30', className: 'N3-K12', teacher: 'Nguyễn Thu Hà', room: 'P.301', tone: 'primary', students: 18 },
  { day: 4, from: '18:00', to: '19:30', className: 'N3-K12', teacher: 'Nguyễn Thu Hà', room: 'P.301', tone: 'primary', students: 18 },
  { day: 1, from: '19:45', to: '21:15', className: 'N2-K05 (Online)', teacher: 'Nguyễn Thu Hà', room: 'Zoom', tone: 'success', students: 12, online: true },
  { day: 3, from: '19:45', to: '21:15', className: 'N2-K05 (Online)', teacher: 'Nguyễn Thu Hà', room: 'Zoom', tone: 'success', students: 12, online: true },
  { day: 5, from: '09:00', to: '11:00', className: 'Kaiwa B1', teacher: 'Nguyễn Thu Hà', room: 'P.205', tone: 'info', students: 10 },
] as const;

export function buildWeekSessions(anyDayInWeek: Dayjs): MockSession[] {
  const ws = weekStartOf(anyDayInWeek);
  return TEMPLATE.map((t, i) => {
    const [fh, fm] = t.from.split(':').map(Number);
    const [th, tm] = t.to.split(':').map(Number);
    const date = ws.add(t.day, 'day');
    const online = 'online' in t && t.online;
    return {
      id: `${ws.format('YYYYMMDD')}-${i}`,
      start: date.hour(fh).minute(fm),
      end: date.hour(th).minute(tm),
      title: t.className,
      subtitle: `GV ${t.teacher}`,
      location: online ? 'Online · Zoom' : `${t.room} · Cầu Giấy`,
      tone: t.tone,
      className: t.className,
      teacher: t.teacher,
      room: t.room,
      online,
      meetingUrl: online ? 'https://zoom.us/j/000000000' : undefined,
      studentCount: t.students,
    };
  });
}

/** Các buổi hôm nay (nếu hôm nay trống thì lấy ngày có lịch gần nhất trong tuần — để demo luôn có dữ liệu) */
export function upcomingSessions(limit = 3): MockSession[] {
  const now = dayjs();
  const list = [...buildWeekSessions(now), ...buildWeekSessions(now.add(7, 'day'))]
    .filter((s) => dayjs(s.end).isAfter(now))
    .sort((a, b) => dayjs(a.start).valueOf() - dayjs(b.start).valueOf());
  return list.slice(0, limit);
}
