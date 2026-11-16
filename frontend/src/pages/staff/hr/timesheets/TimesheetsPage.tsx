import { useMemo, useState } from 'react';
import { Form, Tooltip } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { ChevronLeft, ChevronRight, Clock, LogIn, TriangleAlert } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, ModalForm, PageHeader, Select, TextInput, TimeInput } from '@/components/common';
import { DEPARTMENT_OPTIONS, LATE_GRACE_MIN } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import { DAY_STATE, dayState, meOf, type DayState } from '@/features/hr/hrUtils';
import type { Employee } from '@/features/hr/types';
import { useWorkStore } from '@/features/work/workStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

const CELL: Record<DayState, string> = {
  ok: 'bg-success-50 text-success-700',
  late: 'bg-warning-50 text-warning-700 font-bold',
  early: 'bg-warning-50 text-warning-700 font-bold',
  late_early: 'bg-danger-50 text-danger-700 font-bold',
  no_out: 'bg-danger-50 text-danger-700 font-bold',
  absent: 'bg-danger-600 text-white font-bold',
  leave: 'bg-primary-50 text-primary-700 font-bold',
  off: 'text-ink-muted/40',
  future: '',
  working: 'bg-primary-100 text-primary-700',
};
const atTime = (t?: string) => (t ? dayjs(`2000-01-01T${t}`) : undefined);

/** Chấm công nhân viên (check-in / check-out) — so khớp với ca được xếp. GV chấm công theo buổi dạy (Buổi học). */
export default function TimesheetsPage() {
  const { user, branchId, can } = useAuth();
  const navigate = useNavigate();
  const employees = useHrStore((s) => s.employees);
  const records = useHrStore((s) => s.records);
  const requests = useHrStore((s) => s.requests);
  const editRecord = useHrStore((s) => s.editRecord);
  const shifts = useWorkStore((s) => s.shifts);
  const me = meOf(employees, user);
  const manager = can('hr.timesheets') && user?.roleName !== 'Sale';

  const [month, setMonth] = useState(() => dayjs().startOf('month'));
  const [dept, setDept] = useState<string>();
  const [problemsOnly, setProblemsOnly] = useState(false);
  const [editing, setEditing] = useState<{ e: Employee; date: string } | null>(null);

  const days = Array.from({ length: month.daysInMonth() }, (_, i) => month.add(i, 'day').format('YYYY-MM-DD'));
  const today = dayjs().format('YYYY-MM-DD');

  const people = employees.filter((e) => e.kind === 'staff' && e.status !== 'quit' && (branchId === ALL_BRANCHES || e.branchId === branchId) && (!dept || e.department === dept));
  const table = useMemo(
    () =>
      people.map((e) => {
        const cells = days.map((date) => {
          const rec = records.find((r) => r.employeeId === e.id && r.date === date);
          return { date, rec, ...dayState(e, date, rec, shifts, requests) };
        });
        const c = (s: DayState[]) => cells.filter((x) => s.includes(x.state)).length;
        return {
          e,
          cells,
          work: c(['ok', 'late', 'early', 'late_early', 'no_out', 'working']),
          late: c(['late', 'late_early']),
          early: c(['early', 'late_early']),
          noOut: c(['no_out']),
          absent: c(['absent']),
          leave: c(['leave']),
          lateMin: cells.reduce((n, x) => n + (x.state === 'late' || x.state === 'late_early' ? x.lateMin : 0), 0),
          hours: cells.reduce((n, x) => n + x.worked, 0) / 60,
        };
      }),
    [people, days, records, shifts, requests], // eslint-disable-line react-hooks/exhaustive-deps
  );
  const shown = problemsOnly ? table.filter((t) => t.late || t.early || t.noOut || t.absent) : table;

  const editingRec = editing && records.find((r) => r.employeeId === editing.e.id && r.date === editing.date);

  return (
    <>
      <PageHeader
        title="Chấm công"
        subtitle={`Phòng nhân sự theo dõi công TOÀN BỘ nhân viên văn phòng & làm theo ca — so khớp ca được xếp: đi muộn / về sớm quá ${LATE_GRACE_MIN} phút bị đánh dấu`}
        breadcrumb={[{ title: 'Nhân sự' }, { title: 'Chấm công' }]}
        actions={
          <>
            <Button icon={LogIn} variant="outline" tone="neutral" collapseOnMobile onClick={() => navigate('/admin/me/attendance')}>
              Chấm công của tôi
            </Button>
            <Button icon={Clock} variant="outline" collapseOnMobile onClick={() => navigate('/admin/academic/sessions')}>
              Chấm công giáo viên (theo buổi)
            </Button>
          </>
        }
      />

      <div className="mb-3 flex flex-wrap items-center gap-2">
        <div className="flex items-center gap-1">
          <Button icon={ChevronLeft} tone="neutral" variant="outline" iconOnly aria-label="Tháng trước" onClick={() => setMonth((m) => m.subtract(1, 'month'))} />
          <Button icon={ChevronRight} tone="neutral" variant="outline" iconOnly aria-label="Tháng sau" onClick={() => setMonth((m) => m.add(1, 'month'))} disabled={month.isSame(dayjs(), 'month')} />
        </div>
        <span className="font-semibold text-ink">Tháng {month.format('MM/YYYY')}</span>
        <Select placeholder="Phòng ban" value={dept} onChange={setDept} options={DEPARTMENT_OPTIONS.filter((d) => d.value !== 'teaching')} showSearch={false} className="!w-48" />
        <label className="inline-flex items-center gap-1.5 text-[13px] text-ink-secondary">
          <input type="checkbox" checked={problemsOnly} onChange={(e) => setProblemsOnly(e.target.checked)} /> Chỉ người có vi phạm
        </label>
        <span className="ml-auto flex flex-wrap gap-2 text-[11px] text-ink-secondary">
          {(['ok', 'late', 'early', 'no_out', 'absent', 'leave'] as DayState[]).map((s) => (
            <span key={s} className="inline-flex items-center gap-1">
              <span className={cn('grid h-4 min-w-4 place-items-center rounded px-0.5 text-[10px]', CELL[s])}>{DAY_STATE[s].short}</span>
              {DAY_STATE[s].label}
            </span>
          ))}
        </span>
      </div>

      <div className="overflow-auto rounded-[var(--radius-card)] border border-line bg-surface">
        <table className="w-full border-collapse text-[12px]">
          <thead className="sticky top-0 z-10 bg-primary-50">
            <tr>
              <th className="sticky left-0 z-10 min-w-48 border-b border-line bg-primary-50 px-3 py-2 text-left text-[13px] font-semibold">Nhân viên</th>
              {days.map((d) => {
                const dj = dayjs(d);
                return (
                  <th key={d} className={cn('min-w-7 border-b border-l border-line px-0.5 py-1 font-medium', d === today ? 'bg-primary-100 text-primary-800' : dj.day() === 0 ? 'text-danger-600' : 'text-ink-muted')}>
                    <span className="block text-[10px]">{dj.format('dd')}</span>
                    <span className="tabular">{dj.date()}</span>
                  </th>
                );
              })}
              {['Công', 'Muộn', 'Sớm', 'Quên ra', 'Vắng', 'Phép', 'Giờ'].map((h) => (
                <th key={h} className="border-b border-l border-line px-2 py-1 text-[11px] font-semibold whitespace-nowrap text-ink">
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {shown.map((t) => (
              <tr key={t.e.id} className={cn(me?.id === t.e.id && 'bg-primary-50/40')}>
                <td className="sticky left-0 z-[5] border-b border-line bg-surface px-3 py-1.5">
                  <p className="text-[13px] font-medium text-ink">{t.e.fullName}</p>
                  <p className="text-[11px] text-ink-muted">
                    {t.e.position} · {t.e.shiftBased ? 'theo ca' : 'HC'}
                    {branchId === ALL_BRANCHES && ` · ${getBranches(false).find((b) => b.id === t.e.branchId)?.name}`}
                  </p>
                </td>
                {t.cells.map((c) => (
                  <td key={c.date} className="border-b border-l border-line p-0.5 text-center">
                    {c.state !== 'future' && (c.state !== 'off' || c.rec) ? (
                      <Tooltip
                        title={
                          <span>
                            {dayjs(c.date).format('DD/MM')} · {c.exp ? `${c.exp.label} ${c.exp.from}–${c.exp.to}` : 'Không có ca'}
                            <br />
                            Vào {c.rec?.checkIn ?? '—'} · Ra {c.rec?.checkOut ?? '—'} — {DAY_STATE[c.state].label}
                            {c.lateMin > LATE_GRACE_MIN && ` ${c.lateMin}′`}
                            {c.rec?.edited && (
                              <>
                                <br />
                                Sửa bởi {c.rec.edited.by}: {c.rec.edited.reason}
                              </>
                            )}
                          </span>
                        }
                      >
                        <button
                          type="button"
                          disabled={!manager}
                          onClick={() => setEditing({ e: t.e, date: c.date })}
                          className={cn('grid h-6 w-full place-items-center rounded text-[10px]', CELL[c.state], c.rec?.edited && 'ring-1 ring-primary-400', manager && 'hover:ring-2 hover:ring-primary-300')}
                        >
                          {DAY_STATE[c.state].short || '·'}
                        </button>
                      </Tooltip>
                    ) : (
                      <span className="text-ink-muted/30">·</span>
                    )}
                  </td>
                ))}
                <td className="tabular border-b border-l border-line px-2 text-center font-semibold">{t.work}</td>
                <td className={cn('tabular border-b border-l border-line px-2 text-center', t.late > 0 && 'font-semibold text-warning-700')} title={t.lateMin ? `Tổng ${t.lateMin} phút` : undefined}>
                  {t.late || '—'}
                </td>
                <td className={cn('tabular border-b border-l border-line px-2 text-center', t.early > 0 && 'font-semibold text-warning-700')}>{t.early || '—'}</td>
                <td className={cn('tabular border-b border-l border-line px-2 text-center', t.noOut > 0 && 'font-semibold text-danger-600')}>{t.noOut || '—'}</td>
                <td className={cn('tabular border-b border-l border-line px-2 text-center', t.absent > 0 && 'font-semibold text-danger-600')}>{t.absent || '—'}</td>
                <td className="tabular border-b border-l border-line px-2 text-center text-primary-700">{t.leave || '—'}</td>
                <td className="tabular border-b border-l border-line px-2 text-right">{t.hours.toFixed(0)}h</td>
              </tr>
            ))}
            {!shown.length && (
              <tr>
                <td colSpan={days.length + 8} className="px-3 py-8 text-center text-[13px] text-ink-muted">
                  Không có nhân viên phù hợp
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <p className="mt-2 flex items-center gap-1.5 text-xs text-ink-muted">
        <TriangleAlert size={13} aria-hidden /> Ô viền xanh = công đã được quản lý sửa tay. Bảng công tháng là căn cứ tính Lương nhân viên.
      </p>

      {editing && (
        <ModalForm<{ checkIn?: Dayjs; checkOut?: Dayjs; reason: string }>
          open
          onClose={() => setEditing(null)}
          title={`Sửa công — ${editing.e.fullName}`}
          description={dayjs(editing.date).format('dddd, DD/MM/YYYY')}
          submitAction="save"
          submitText="Lưu"
          successMessage="Đã sửa công"
          initialValues={{ checkIn: atTime(editingRec?.checkIn), checkOut: atTime(editingRec?.checkOut) }}
          onSubmit={(v) => editRecord(editing.e.id, editing.date, { checkIn: v.checkIn?.format('HH:mm'), checkOut: v.checkOut?.format('HH:mm') }, user?.fullName ?? 'Quản lý', v.reason.trim())}
        >
          <div className="grid grid-cols-2 gap-x-3">
            <Form.Item name="checkIn" label="Giờ vào">
              <TimeInput minuteStep={1} />
            </Form.Item>
            <Form.Item name="checkOut" label="Giờ ra">
              <TimeInput minuteStep={1} />
            </Form.Item>
          </div>
          <Form.Item name="reason" label="Lý do sửa" rules={[{ required: true, whitespace: true, message: 'Bắt buộc ghi lý do (lưu vết)' }]}>
            <TextInput placeholder="VD: Quên check-out, đã xác nhận với quản lý ca" />
          </Form.Item>
        </ModalForm>
      )}
    </>
  );
}
