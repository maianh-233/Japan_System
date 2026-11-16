import { useMemo } from 'react';
import { Progress } from 'antd';
import dayjs from 'dayjs';
import { CalendarClock, MapPin, School, TriangleAlert, Users, Video } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { CLASS_STATUS, scheduleLabel, totalSessionsOf } from '@/features/academic/classes/constants';
import { useAllStudentStats } from '@/features/academic/classes/studentInsights';
import { isTaught, notStarted } from '@/features/academic/classes/sessionUtils';
import { shortCode, useTeachingClasses } from '@/features/portal/usePortal';
import { cn } from '@/lib/cn';

/** Cổng GV › Lớp của tôi: các lớp đang dạy + lớp đã dạy xong */
export default function MyClassesPage() {
  const navigate = useNavigate();
  const classes = useTeachingClasses();
  const sessions = useClassStore((s) => s.sessions);
  const stats = useAllStudentStats(classes);

  const cards = useMemo(
    () =>
      stats
        .map(({ cls, map }) => {
          const own = sessions.filter((s) => s.classId === cls.id);
          const next = own.filter((s) => s.status !== 'cancelled' && notStarted(s)).sort((a, b) => (a.date + a.startTime).localeCompare(b.date + b.startTime))[0];
          const list = [...map.values()].filter((x) => x.enrollment.status === 'studying' || x.enrollment.status === 'reserved');
          return {
            cls,
            taught: own.filter(isTaught).length,
            total: totalSessionsOf(cls),
            students: list.length,
            flagged: list.filter((x) => x.flags.length).length,
            noLog: own.filter((s) => s.status === 'completed' && !s.teacherLog).length,
            next,
          };
        })
        .sort((a, b) => Number(b.cls.status === 'ongoing') - Number(a.cls.status === 'ongoing')),
    [stats, sessions],
  );

  return (
    <>
      <PageHeader title="Lớp của tôi" subtitle="Bấm vào lớp để mở buổi học: nội dung cần dạy, điểm danh, nhật ký buổi, theo dõi học viên." />
      {!cards.length && <EmptyState icon={School} title="Bạn chưa được phân công lớp nào" />}
      <div className="grid gap-4 md:grid-cols-2">
        {cards.map(({ cls, taught, total, students, flagged, noLog, next }) => (
          <button
            key={cls.id}
            type="button"
            onClick={() => navigate(`/teacher/classes/${cls.id}`)}
            className={cn('flex flex-col rounded-xl border bg-surface p-4 text-left transition-colors hover:border-primary-300', cls.status === 'finished' ? 'border-dashed border-line opacity-80' : 'border-line')}
          >
            <div className="flex items-start justify-between gap-2">
              <div>
                <p className="text-base font-semibold text-ink">{shortCode(cls)}</p>
                <p className="text-[13px] text-ink-secondary">{cls.courseLabel}</p>
              </div>
              <StatusTag tone={CLASS_STATUS[cls.status].tone} icon={CLASS_STATUS[cls.status].icon} size="sm">
                {CLASS_STATUS[cls.status].label}
              </StatusTag>
            </div>
            <p className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-[13px] text-ink-secondary">
              <span className="inline-flex items-center gap-1">
                <CalendarClock size={14} aria-hidden /> {scheduleLabel(cls)}
              </span>
              <span className="inline-flex items-center gap-1">
                {cls.online ? <Video size={14} aria-hidden /> : <MapPin size={14} aria-hidden />} {cls.online ? 'Online' : `Phòng ${cls.room ?? '—'}`}
              </span>
              <span className="inline-flex items-center gap-1">
                <Users size={14} aria-hidden /> {students} học viên
              </span>
            </p>
            <div className="mt-3">
              <div className="flex justify-between text-xs text-ink-muted">
                <span>Tiến độ</span>
                <span className="tabular">
                  {taught}/{total} buổi
                </span>
              </div>
              <Progress percent={Math.round((taught / Math.max(1, total)) * 100)} showInfo={false} size="small" />
            </div>
            <div className="mt-2 flex flex-wrap gap-2 text-xs">
              {next && (
                <span className="rounded-full bg-primary-50 px-2.5 py-1 text-primary-700">
                  Buổi tới: {dayjs(next.date).format('dd DD/MM')} {next.startTime}
                  {next.kind === 'exam' && ' · THI'}
                </span>
              )}
              {flagged > 0 && (
                <span className="inline-flex items-center gap-1 rounded-full bg-warning-50 px-2.5 py-1 text-warning-700">
                  <TriangleAlert size={12} aria-hidden /> {flagged} HV cần chú ý
                </span>
              )}
              {noLog > 0 && <span className="rounded-full bg-danger-50 px-2.5 py-1 text-danger-700">{noLog} buổi chưa ghi nhật ký</span>}
            </div>
          </button>
        ))}
      </div>
    </>
  );
}
