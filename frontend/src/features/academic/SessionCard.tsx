import type { ReactNode } from 'react';
import dayjs from 'dayjs';
import { Clock, MapPin, Users, Video } from 'lucide-react';
import { cn } from '@/lib/cn';
import { accentBorderClasses } from '@/components/common';
import type { MockSession } from '@/mocks/data/schedule';

/** Thẻ một buổi học: giờ · lớp · phòng/online · sĩ số + vùng nút hành động */
export function SessionCard({ session, actions, showTeacher }: { session: MockSession; actions?: ReactNode; showTeacher?: boolean }) {
  const s = dayjs(session.start);
  const e = dayjs(session.end);
  const isToday = s.isSame(dayjs(), 'day');
  return (
    <div
      className={cn(
        'flex flex-col gap-3 rounded-[var(--radius-card)] border border-l-4 border-line bg-surface p-4 sm:flex-row sm:items-center',
        accentBorderClasses[session.tone ?? 'primary'],
      )}
    >
      <div className="flex shrink-0 flex-col sm:w-24">
        <span className="text-xs font-medium text-ink-muted">{isToday ? 'Hôm nay' : s.format('dddd')}</span>
        <span className="tabular text-lg font-semibold text-ink">{s.format('HH:mm')}</span>
        <span className="tabular text-xs text-ink-muted">đến {e.format('HH:mm')}</span>
      </div>
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <span className="font-semibold text-ink">{session.className}</span>
        <div className="flex flex-wrap gap-x-4 gap-y-1 text-[13px] text-ink-secondary">
          <span className="flex items-center gap-1.5">
            {session.online ? <Video size={14} aria-hidden /> : <MapPin size={14} aria-hidden />}
            {session.location}
          </span>
          <span className="flex items-center gap-1.5">
            <Users size={14} aria-hidden />
            {session.studentCount} học viên
          </span>
          {showTeacher && (
            <span className="flex items-center gap-1.5">
              <Clock size={14} aria-hidden />
              {session.subtitle}
            </span>
          )}
        </div>
      </div>
      {actions && <div className="flex flex-wrap gap-2 sm:justify-end">{actions}</div>}
    </div>
  );
}
