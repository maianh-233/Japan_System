import { useMemo, useState } from 'react';
import { Download, Search } from 'lucide-react';
import { Button, EmptyState, Select, TextInput, useNotify } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { ENROLLMENT_STATUS } from '@/features/student/constants';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { ALERT_RULES, useClassStudentStats } from '../studentInsights';
import type { ClassInfo } from '../types';
import { SessionDetailDrawer } from './SessionDetailDrawer';
import { ATT_CELL, StudentLearningDrawer } from './StudentLearningDrawer';

type Filter = 'all' | 'low' | 'absent' | 'active';

/** Tab Chuyên cần: ma trận học viên × buổi (đã điểm danh), tỉ lệ từng học viên + từng buổi, xuất Excel */
export function ClassAttendanceTab({ cls }: { cls: ClassInfo }) {
  const { can } = useAuth();
  const notify = useNotify();
  const { map, sessions } = useClassStudentStats(cls);
  const students = useCrmStore((s) => s.students);
  const [filter, setFilter] = useState<Filter>('active');
  const [q, setQ] = useState('');
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [profileId, setProfileId] = useState<string | null>(null);
  const editable = can('academic.classes') && cls.status !== 'finished' && cls.status !== 'cancelled';

  const rows = useMemo(() => {
    const kw = q.trim().toLowerCase();
    return [...map.values()]
      .map((st) => ({ st, student: students.find((x) => x.id === st.enrollment.studentId) }))
      .filter(({ st, student }) => {
        if (kw && !`${student?.fullName} ${student?.code}`.toLowerCase().includes(kw)) return false;
        if (filter === 'active') return st.enrollment.status === 'studying' || st.enrollment.status === 'reserved' || st.enrollment.status === 'completed';
        if (filter === 'low') return st.attendance.rate !== undefined && st.attendance.rate < ALERT_RULES.attendanceRate;
        if (filter === 'absent') return st.attendance.absent > 0;
        return true;
      })
      .sort((a, b) => (a.student?.fullName ?? '').localeCompare(b.student?.fullName ?? '', 'vi'));
  }, [map, students, q, filter]);

  const all = [...map.values()].filter((s) => s.attendance.sessions);
  const avg = all.length ? Math.round(all.reduce((n, s) => n + (s.attendance.rate ?? 0), 0) / all.length) : undefined;

  const exportCsv = () => {
    const head = ['Mã HV', 'Họ tên', 'Trạng thái', ...sessions.map((s) => `${s.no ? `B${s.no}` : 'Thêm'} ${s.date}`), 'Có mặt', 'Muộn', 'Có phép', 'Vắng KP', 'Chuyên cần %'];
    const lines = rows.map(({ st, student }) => [
      student?.code ?? '',
      student?.fullName ?? st.enrollment.studentId,
      ENROLLMENT_STATUS[st.enrollment.status].label,
      ...st.attendance.timeline.map((t) => (t.status ? ATT_CELL[t.status].short : '')),
      st.attendance.present,
      st.attendance.late,
      st.attendance.excused,
      st.attendance.absent,
      st.attendance.rate ?? '',
    ]);
    const csv = [head, ...lines].map((l) => l.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `chuyen-can-${cls.code}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    notify.toast.success('Đã xuất file chuyên cần (mở bằng Excel)');
  };

  if (!sessions.length) return <EmptyState size="compact" title="Chưa có buổi nào được điểm danh" description="Điểm danh ở tab Buổi học — bảng chuyên cần sẽ tự tổng hợp." />;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <TextInput icon={Search} placeholder="Tìm học viên..." value={q} onChange={(e) => setQ(e.target.value)} allowClear className="sm:!w-64" />
        <Select<Filter>
          value={filter}
          onChange={setFilter}
          showSearch={false}
          className="!w-52"
          options={[
            { value: 'active', label: 'Học viên trong lớp' },
            { value: 'low', label: `Chuyên cần < ${ALERT_RULES.attendanceRate}%` },
            { value: 'absent', label: 'Có vắng không phép' },
            { value: 'all', label: 'Tất cả (kể cả đã nghỉ)' },
          ]}
        />
        <span className="tabular text-[13px] text-ink-muted">
          {sessions.length} buổi đã điểm danh · chuyên cần TB lớp <strong className={cn(avg !== undefined && avg < ALERT_RULES.attendanceRate ? 'text-danger-600' : 'text-ink')}>{avg ?? '—'}%</strong>
        </span>
        <Button icon={Download} variant="outline" collapseOnMobile className="ml-auto" onClick={exportCsv}>
          Xuất Excel
        </Button>
      </div>

      <div className="flex flex-wrap gap-3 text-xs text-ink-muted">
        {Object.values(ATT_CELL).map((m) => (
          <span key={m.label} className="flex items-center gap-1">
            <span className={cn('inline-flex h-5 w-5 items-center justify-center rounded', m.cls)}>{m.short}</span> {m.label}
          </span>
        ))}
        <span>· Bấm tiêu đề buổi để sửa điểm danh, bấm tên để xem hồ sơ học tập</span>
      </div>

      <div className="overflow-x-auto rounded-[var(--radius-card)] border border-line">
        <table className="w-max min-w-full border-collapse text-[13px]">
          <thead>
            <tr className="bg-page">
              <th className="sticky left-0 z-10 min-w-48 border-b border-line bg-page px-3 py-2 text-left font-medium text-ink-secondary">Học viên</th>
              {sessions.map((s) => (
                <th key={s.id} className="border-b border-l border-line px-0.5 py-1 font-normal">
                  <button
                    type="button"
                    onClick={() => setSessionId(s.id)}
                    title={`Buổi ${s.no || '(học thêm)'} · ${s.date}`}
                    className="tabular flex w-9 flex-col items-center rounded py-0.5 text-[11px] leading-tight text-ink-secondary hover:bg-primary-50 hover:text-primary-700"
                  >
                    <span className="font-semibold">{s.no || '+'}</span>
                    <span className="text-[10px] text-ink-muted">{s.date.slice(8, 10)}/{s.date.slice(5, 7)}</span>
                  </button>
                </th>
              ))}
              {['Có mặt', 'Muộn', 'Phép', 'Vắng', '%'].map((h) => (
                <th key={h} className="border-b border-l border-line px-2 py-2 text-center font-medium text-ink-secondary">
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map(({ st, student }) => {
              const a = st.attendance;
              const low = a.rate !== undefined && a.rate < ALERT_RULES.attendanceRate;
              const gone = st.enrollment.status === 'dropped' || st.enrollment.status === 'transferred';
              return (
                <tr key={st.enrollment.id} className={cn('hover:bg-page/60', gone && 'opacity-55')}>
                  <td className="sticky left-0 z-10 border-b border-line bg-surface px-3 py-1.5">
                    <button type="button" onClick={() => setProfileId(st.enrollment.studentId)} className="flex flex-col text-left">
                      <span className="font-medium text-ink hover:text-primary-700">{student?.fullName ?? st.enrollment.studentId}</span>
                      <span className="text-[11px] text-ink-muted">
                        {student?.code}
                        {st.enrollment.status !== 'studying' && ` · ${ENROLLMENT_STATUS[st.enrollment.status].label}`}
                      </span>
                    </button>
                  </td>
                  {a.timeline.map((t) => {
                    const m = t.status ? ATT_CELL[t.status] : undefined;
                    return (
                      <td key={t.sessionId} className="border-b border-l border-line p-0.5 text-center" title={m ? `${m.label}${t.note ? ` — ${t.note}` : ''}` : 'Không thuộc buổi này'}>
                        <span className={cn('mx-auto flex h-7 w-8 items-center justify-center rounded text-xs', m?.cls ?? 'text-line')}>{m?.short ?? '·'}</span>
                      </td>
                    );
                  })}
                  <td className="tabular border-b border-l border-line px-2 text-center">{a.present}</td>
                  <td className="tabular border-b border-l border-line px-2 text-center">{a.late || ''}</td>
                  <td className="tabular border-b border-l border-line px-2 text-center">{a.excused || ''}</td>
                  <td className={cn('tabular border-b border-l border-line px-2 text-center', a.absent >= ALERT_RULES.absent && 'font-semibold text-danger-600')}>{a.absent || ''}</td>
                  <td className={cn('tabular border-b border-l border-line px-2 text-center font-semibold', low ? 'text-danger-600' : 'text-ink')}>{a.rate ?? '—'}</td>
                </tr>
              );
            })}
          </tbody>
          <tfoot>
            <tr className="bg-page">
              <td className="sticky left-0 z-10 bg-page px-3 py-1.5 text-xs font-medium text-ink-secondary">Có mặt / buổi</td>
              {sessions.map((s) => {
                const r = s.attendance && s.attendance.total ? Math.round((s.attendance.present / s.attendance.total) * 100) : undefined;
                return (
                  <td key={s.id} className={cn('tabular border-l border-line text-center text-[11px]', r !== undefined && r < ALERT_RULES.attendanceRate ? 'font-semibold text-danger-600' : 'text-ink-muted')}>
                    {r ?? '—'}
                  </td>
                );
              })}
              <td colSpan={5} className="border-l border-line" />
            </tr>
          </tfoot>
        </table>
      </div>
      {!rows.length && <p className="py-4 text-center text-[13px] text-ink-muted">Không có học viên phù hợp bộ lọc.</p>}

      <SessionDetailDrawer
        cls={cls}
        sessionId={sessionId}
        initialTab="students"
        editable={editable}
        onClose={() => setSessionId(null)}
        onOpen={setSessionId}
        onReschedule={() => notify.toast.info('Đổi lịch ở tab Buổi học')}
        onCancel={() => notify.toast.info('Hủy buổi ở tab Buổi học')}
        onMakeup={() => notify.toast.info('Xếp học bù ở tab Buổi học')}
      />
      <StudentLearningDrawer cls={cls} studentId={profileId} onClose={() => setProfileId(null)} />
    </div>
  );
}
