import dayjs, { type Dayjs } from 'dayjs';
import { isOpenClass } from '../classes/constants';
import { sessionRoom } from '../classes/sessionUtils';
import type { ClassInfo, ClassSession } from '../classes/types';
import { OPEN_HOURS, PEAK } from './constants';
import type { Room } from './types';

/** 1 lần sử dụng phòng = 1 buổi học (đã áp dụng đổi phòng của buổi) */
export interface RoomBooking {
  session: ClassSession;
  cls: ClassInfo;
}

const toMin = (hhmm: string) => {
  const [h, m] = hhmm.split(':').map(Number);
  return h * 60 + m;
};
const overlapMin = (aStart: string, aEnd: string, bStart: string, bEnd: string) =>
  Math.max(0, Math.min(toMin(aEnd), toMin(bEnd)) - Math.max(toMin(aStart), toMin(bStart)));

export const roomKey = (branchId: string, name: string) => `${branchId}::${name}`;

/** Gom buổi học theo phòng (bỏ lớp online) — tính 1 lần cho cả trang */
export function bookingsByRoom(classes: ClassInfo[], sessions: ClassSession[]): Map<string, RoomBooking[]> {
  const byId = new Map(classes.map((c) => [c.id, c]));
  const out = new Map<string, RoomBooking[]>();
  for (const s of sessions) {
    const cls = byId.get(s.classId);
    if (!cls) continue;
    const room = sessionRoom(s, cls);
    if (!room) continue;
    const k = roomKey(cls.branchId, room);
    const list = out.get(k);
    if (list) list.push({ session: s, cls });
    else out.set(k, [{ session: s, cls }]);
  }
  return out;
}

/** Lớp đang mở (chờ khai giảng / đang học / tạm dừng) lấy phòng này làm phòng mặc định */
export const openClassesIn = (room: Pick<Room, 'branchId' | 'name'>, classes: ClassInfo[]) =>
  classes.filter((c) => !c.online && c.branchId === room.branchId && c.room === room.name && isOpenClass(c));

export interface Occupancy {
  /** Số giờ có lớp */
  hours: number;
  /** Số giờ mở cửa trong kỳ */
  openHours: number;
  pct: number;
  /** % lấp đầy khung giờ cao điểm */
  peakPct: number;
  sessions: number;
}

/** Tỷ lệ lấp đầy trong [from, from + days) — chỉ tính buổi không bị hủy */
export function occupancyOf(bookings: RoomBooking[], from: Dayjs, days = 7): Occupancy {
  const start = from.format('YYYY-MM-DD');
  const end = from.add(days, 'day').format('YYYY-MM-DD');
  const inRange = bookings.filter((b) => b.session.status !== 'cancelled' && b.session.date >= start && b.session.date < end);
  const minutes = inRange.reduce((s, b) => s + overlapMin(b.session.startTime, b.session.endTime, OPEN_HOURS.start, OPEN_HOURS.end), 0);
  const peak = inRange.reduce((s, b) => s + overlapMin(b.session.startTime, b.session.endTime, PEAK.start, PEAK.end), 0);
  const openMin = (toMin(OPEN_HOURS.end) - toMin(OPEN_HOURS.start)) * days;
  const peakMin = (toMin(PEAK.end) - toMin(PEAK.start)) * days;
  return {
    hours: Math.round((minutes / 60) * 10) / 10,
    openHours: openMin / 60,
    pct: Math.min(100, Math.round((minutes / openMin) * 100)),
    peakPct: Math.min(100, Math.round((peak / peakMin) * 100)),
    sessions: inRange.length,
  };
}

/** Các cặp buổi trùng giờ trong cùng phòng, từ hôm nay trở đi */
export function roomConflicts(bookings: RoomBooking[]): [RoomBooking, RoomBooking][] {
  const today = dayjs().format('YYYY-MM-DD');
  const live = bookings
    .filter((b) => b.session.status !== 'cancelled' && b.session.date >= today)
    .sort((a, b) => a.session.date.localeCompare(b.session.date) || a.session.startTime.localeCompare(b.session.startTime));
  const out: [RoomBooking, RoomBooking][] = [];
  for (let i = 0; i < live.length; i++) {
    for (let j = i + 1; j < live.length && live[j].session.date === live[i].session.date; j++) {
      const a = live[i].session;
      const b = live[j].session;
      if (overlapMin(a.startTime, a.endTime, b.startTime, b.endTime) > 0) out.push([live[i], live[j]]);
    }
  }
  return out;
}

/** Thứ 2 đầu tuần */
export const weekStartOf = (d: Dayjs) => d.subtract((d.day() + 6) % 7, 'day').startOf('day');
