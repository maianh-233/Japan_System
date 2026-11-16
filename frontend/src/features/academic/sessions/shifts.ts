import { Moon, Sun, Sunrise, type LucideIcon } from 'lucide-react';

/** Ca học trong ngày — buổi học thuộc ca theo GIỜ BẮT ĐẦU */
export interface Shift {
  key: 'morning' | 'afternoon' | 'evening';
  label: string;
  from: string;
  to: string;
  icon: LucideIcon;
}

export const SHIFTS: Shift[] = [
  { key: 'morning', label: 'Ca sáng', from: '07:00', to: '12:00', icon: Sunrise },
  { key: 'afternoon', label: 'Ca chiều', from: '12:00', to: '17:00', icon: Sun },
  { key: 'evening', label: 'Ca tối', from: '17:00', to: '22:00', icon: Moon },
];

export const shiftOf = (startTime: string): Shift => SHIFTS.find((s) => startTime >= s.from && startTime < s.to) ?? (startTime < SHIFTS[0].from ? SHIFTS[0] : SHIFTS[2]);

export const toMin = (t: string) => {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
};

/** Xếp các khoảng thời gian trùng nhau vào các làn (ít làn nhất) */
export function packLanes<T extends { start: string; end: string }>(items: T[]): (T & { lane: number })[] {
  const sorted = [...items].sort((a, b) => a.start.localeCompare(b.start) || b.end.localeCompare(a.end));
  const laneEnds: string[] = [];
  return sorted.map((it) => {
    let lane = laneEnds.findIndex((end) => end <= it.start);
    if (lane < 0) {
      lane = laneEnds.length;
      laneEnds.push(it.end);
    } else laneEnds[lane] = it.end;
    return { ...it, lane };
  });
}
