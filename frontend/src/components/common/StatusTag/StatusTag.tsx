import type { ReactNode } from 'react';
import { CircleHelp, type LucideIcon } from 'lucide-react';
import { cn } from '@/lib/cn';
import { softToneClasses, type Tone } from '../tones';
import { STATUS_PRESETS, type StatusGroup, type StatusOf, type StatusPreset } from './statusPresets';

type Size = 'sm' | 'md';

type PresetProps<G extends StatusGroup> = {
  /** Nhóm trạng thái: lead, class, session, invoice, request, attendance, account */
  group: G;
  /** Mã trạng thái — có gợi ý tự động; mã lạ sẽ hiện nguyên văn màu xám */
  status: StatusOf<G> | (string & {});
  tone?: never;
  icon?: never;
  children?: never;
};

type CustomProps = {
  group?: never;
  status?: never;
  tone: Tone;
  icon: LucideIcon;
  children: ReactNode;
};

export type StatusTagProps<G extends StatusGroup = StatusGroup> = (PresetProps<G> | CustomProps) & {
  size?: Size;
  className?: string;
};

/**
 * Nhãn trạng thái có MÀU + ICON + CHỮ (không truyền đạt chỉ bằng màu).
 *  <StatusTag group="invoice" status="paid" />
 *  <StatusTag tone="warning" icon={Clock}>Sắp hết hạn</StatusTag>
 */
export function StatusTag<G extends StatusGroup>(props: StatusTagProps<G>) {
  const { size = 'md', className } = props;
  let preset: StatusPreset;

  if (props.group) {
    const groupPresets = STATUS_PRESETS[props.group] as Record<string, StatusPreset>;
    preset = groupPresets[props.status as string] ?? {
      label: String(props.status),
      tone: 'neutral',
      icon: CircleHelp,
    };
  } else {
    preset = { label: '', tone: props.tone as Tone, icon: props.icon as LucideIcon };
  }

  const Icon = preset.icon;
  return (
    <span
      className={cn(
        'inline-flex max-w-full items-center gap-1 whitespace-nowrap rounded-md border font-medium',
        size === 'sm' ? 'h-6 px-1.5 text-xs' : 'h-7 px-2 text-[13px]',
        softToneClasses[preset.tone],
        className,
      )}
    >
      <Icon size={size === 'sm' ? 12 : 14} aria-hidden className="shrink-0" />
      <span className="truncate">{props.group ? preset.label : props.children}</span>
    </span>
  );
}
