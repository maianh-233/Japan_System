import { CheckCircle2, Circle, CircleDot, Eye, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import type { TaskPriority, TaskRepeat, TaskStatus } from './types';

export const TASK_STATUS: Record<TaskStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  todo: { label: 'Cần làm', tone: 'neutral', icon: Circle },
  doing: { label: 'Đang làm', tone: 'primary', icon: CircleDot },
  review: { label: 'Chờ duyệt', tone: 'warning', icon: Eye },
  done: { label: 'Hoàn thành', tone: 'success', icon: CheckCircle2 },
};
export const TASK_STATUS_ORDER: TaskStatus[] = ['todo', 'doing', 'review', 'done'];

export const TASK_PRIORITY: Record<TaskPriority, { label: string; tone: Tone; bar: string }> = {
  urgent: { label: 'Khẩn cấp', tone: 'danger', bar: 'bg-danger-600' },
  high: { label: 'Cao', tone: 'warning', bar: 'bg-warning-600' },
  normal: { label: 'Bình thường', tone: 'info', bar: 'bg-primary-400' },
  low: { label: 'Thấp', tone: 'neutral', bar: 'bg-line' },
};
export const TASK_PRIORITY_OPTIONS = (['urgent', 'high', 'normal', 'low'] as TaskPriority[]).map((k) => ({ value: k, label: TASK_PRIORITY[k].label }));

export const TASK_REPEAT: Record<TaskRepeat, string> = { none: 'Không lặp', daily: 'Hằng ngày', weekly: 'Hằng tuần', monthly: 'Hằng tháng' };
export const TASK_REPEAT_OPTIONS = (Object.keys(TASK_REPEAT) as TaskRepeat[]).map((k) => ({ value: k, label: TASK_REPEAT[k] }));

export const PRIORITY_RANK: Record<TaskPriority, number> = { urgent: 0, high: 1, normal: 2, low: 3 };

/** Số người tối thiểu mỗi ca theo bộ phận (trong tuần / cuối tuần) */
export const COVERAGE: Partial<Record<'sales' | 'reception', { weekday: Record<'morning' | 'afternoon' | 'evening', number>; weekend: Record<'morning' | 'afternoon' | 'evening', number> }>> = {
  sales: { weekday: { morning: 1, afternoon: 1, evening: 2 }, weekend: { morning: 1, afternoon: 1, evening: 1 } },
  reception: { weekday: { morning: 1, afternoon: 1, evening: 0 }, weekend: { morning: 1, afternoon: 0, evening: 0 } },
};
