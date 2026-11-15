import {
  Ban,
  CalendarClock,
  CheckCircle2,
  CircleDashed,
  CircleDot,
  Clock,
  Hourglass,
  MessageCircle,
  PauseCircle,
  PlayCircle,
  Repeat,
  Sparkles,
  TriangleAlert,
  Undo2,
  UserCheck,
  UserX,
  XCircle,
  type LucideIcon,
} from 'lucide-react';
import type { Tone } from '../tones';

export interface StatusPreset {
  label: string;
  tone: Tone;
  icon: LucideIcon;
}

/**
 * Trạng thái nghiệp vụ → nhãn + màu + icon (khớp docs/design-system.md mục 1.4).
 * Key là mã trạng thái backend sẽ trả về.
 */
export const STATUS_PRESETS = {
  lead: {
    new: { label: 'Mới', tone: 'info', icon: Sparkles },
    consulting: { label: 'Đang tư vấn', tone: 'primary', icon: MessageCircle },
    test_scheduled: { label: 'Hẹn test', tone: 'warning', icon: CalendarClock },
    won: { label: 'Đã chốt', tone: 'success', icon: CheckCircle2 },
    lost: { label: 'Mất', tone: 'neutral', icon: XCircle },
  },
  class: {
    upcoming: { label: 'Sắp mở', tone: 'info', icon: CalendarClock },
    ongoing: { label: 'Đang học', tone: 'success', icon: PlayCircle },
    paused: { label: 'Tạm dừng', tone: 'warning', icon: PauseCircle },
    cancelled: { label: 'Đã hủy', tone: 'danger', icon: Ban },
    finished: { label: 'Kết thúc', tone: 'neutral', icon: CheckCircle2 },
  },
  session: {
    scheduled: { label: 'Đã lên lịch', tone: 'info', icon: CalendarClock },
    in_progress: { label: 'Đang diễn ra', tone: 'primary', icon: CircleDot },
    completed: { label: 'Hoàn thành', tone: 'success', icon: CheckCircle2 },
    cancelled: { label: 'Đã hủy', tone: 'danger', icon: Ban },
    makeup: { label: 'Học bù', tone: 'warning', icon: Repeat },
  },
  invoice: {
    unpaid: { label: 'Chưa thu', tone: 'warning', icon: Clock },
    partial: { label: 'Thu một phần', tone: 'info', icon: CircleDashed },
    paid: { label: 'Đã thu', tone: 'success', icon: CheckCircle2 },
    overdue: { label: 'Quá hạn', tone: 'danger', icon: TriangleAlert },
    refunded: { label: 'Hoàn tiền', tone: 'neutral', icon: Undo2 },
    cancelled: { label: 'Đã hủy', tone: 'neutral', icon: Ban },
  },
  request: {
    pending: { label: 'Chờ duyệt', tone: 'warning', icon: Hourglass },
    approved: { label: 'Đã duyệt', tone: 'success', icon: CheckCircle2 },
    rejected: { label: 'Từ chối', tone: 'danger', icon: XCircle },
    withdrawn: { label: 'Đã rút', tone: 'neutral', icon: Undo2 },
  },
  attendance: {
    present: { label: 'Có mặt', tone: 'success', icon: UserCheck },
    late: { label: 'Đi muộn', tone: 'warning', icon: Clock },
    excused: { label: 'Vắng có phép', tone: 'info', icon: CalendarClock },
    absent: { label: 'Vắng không phép', tone: 'danger', icon: UserX },
  },
  account: {
    active: { label: 'Đang hoạt động', tone: 'success', icon: CheckCircle2 },
    inactive: { label: 'Ngừng hoạt động', tone: 'neutral', icon: PauseCircle },
    locked: { label: 'Bị khóa', tone: 'danger', icon: Ban },
  },
} as const satisfies Record<string, Record<string, StatusPreset>>;

export type StatusGroup = keyof typeof STATUS_PRESETS;
export type StatusOf<G extends StatusGroup> = keyof (typeof STATUS_PRESETS)[G];
