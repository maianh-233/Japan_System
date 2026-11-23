import type { DayState } from './hrUtils';

/**
 * Chế độ API: trạng thái chấm công từng ngày do SERVER tính (giờ phải làm theo ca thật, đơn đã duyệt, giờ ân hạn ở Cài đặt chung).
 * `dayState()` (hrUtils) ưu tiên bản này khi có → mọi trang (chấm công, lương NV, báo cáo, hồ sơ) thống nhất với backend.
 * Ghi trước khi cập nhật `useHrStore.records` để trang vẽ lại đọc được ngay.
 */
export interface ServerDay {
  state: DayState;
  exp?: { from: string; to: string; label: string };
  lateMin: number;
  earlyMin: number;
  worked: number;
}

const days = new Map<string, ServerDay>();
const key = (employeeId: string, date: string) => `${employeeId}|${date}`;

export const serverDayOf = (employeeId: string, date: string) => days.get(key(employeeId, date));

export const setServerDay = (employeeId: string, date: string, day: ServerDay) => days.set(key(employeeId, date), day);
