/** Tông màu dùng chung cho Tag, StatCard, sự kiện Timetable, ConfirmDialog... */
export type Tone = 'primary' | 'info' | 'success' | 'warning' | 'danger' | 'neutral';

/** Nền nhạt + chữ đậm + viền — tất cả cặp màu đạt WCAG AA */
export const softToneClasses: Record<Tone, string> = {
  primary: 'bg-primary-100 text-primary-700 border-primary-200',
  info: 'bg-primary-50 text-primary-700 border-primary-200',
  success: 'bg-success-50 text-success-700 border-success-100',
  warning: 'bg-warning-50 text-warning-700 border-warning-100',
  danger: 'bg-danger-50 text-danger-700 border-danger-100',
  neutral: 'bg-disabled text-ink-secondary border-line',
};

/** Ô icon tròn/vuông nền nhạt */
export const iconToneClasses: Record<Tone, string> = {
  primary: 'bg-primary-100 text-primary-600',
  info: 'bg-primary-50 text-primary-600',
  success: 'bg-success-50 text-success-600',
  warning: 'bg-warning-50 text-warning-600',
  danger: 'bg-danger-50 text-danger-600',
  neutral: 'bg-disabled text-ink-secondary',
};

/** Viền trái nhấn màu (thẻ sự kiện, thẻ cảnh báo) */
export const accentBorderClasses: Record<Tone, string> = {
  primary: 'border-l-primary-600',
  info: 'border-l-primary-400',
  success: 'border-l-success-600',
  warning: 'border-l-warning-600',
  danger: 'border-l-danger-600',
  neutral: 'border-l-line-strong',
};
