import { useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { CalendarCheck, Clock, Hourglass, TriangleAlert, Wallet } from 'lucide-react';
import { DataTable, PageHeader, Select, StatCard, StatusTag, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { sessionTeachers } from '@/features/academic/classes/sessionUtils';
import { useFinanceStore } from '@/features/finance/financeStore';
import { monthOptions, teacherPayroll } from '@/features/finance/financeUtils';
import { useHrStore } from '@/features/hr/hrStore';
import { shortCode, useTeacherId } from '@/features/portal/usePortal';
import { formatCurrency } from '@/lib/format';

const ROLE: Record<string, string> = { teacher: 'GV chính', substitute: 'Dạy thay', assistant: 'Trợ giảng' };

/** Cổng GV › Chấm công: buổi đã dạy trong tháng, giờ công, lương TẠM TÍNH (chốt cuối tháng ở Tài chính) */
export default function TeacherTimesheetPage() {
  const me = useTeacherId();
  const [month, setMonth] = useState(dayjs().format('YYYY-MM'));
  const employees = useHrStore((s) => s.employees);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const adjusts = useFinanceStore((s) => s.adjusts);

  const pay = useMemo(() => teacherPayroll(month, employees, classes, sessions, adjusts).find((p) => p.employee.id === me), [month, employees, classes, sessions, adjusts, me]);
  type Item = NonNullable<typeof pay>['items'][number];

  // Buổi đã qua của mình mà thiếu check-in / check-out
  const missing = useMemo(
    () =>
      sessions.filter((s) => {
        const cls = classes.find((c) => c.id === s.classId);
        if (!cls || !dayjs(s.date).isSame(dayjs(month + '-01'), 'month') || s.status === 'cancelled' || !dayjs(`${s.date}T${s.endTime}`).isBefore(dayjs())) return false;
        if (!sessionTeachers(s, cls).includes(me)) return false;
        const t = s.teacherAttendance?.find((x) => x.teacherId === me);
        return !t || !t.checkIn || !t.checkOut;
      }),
    [sessions, classes, month, me],
  );

  const columns: DataColumn<Item>[] = [
    { key: 'date', title: 'Ngày', width: 110, render: (_, r) => <span className="tabular">{dayjs(r.session.date).format('dd DD/MM')}</span> },
    { key: 'cls', title: 'Lớp', width: 160, mobile: 'title', render: (_, r) => `${shortCode(r.cls)} · buổi ${r.session.no}` },
    { key: 'role', title: 'Vai trò', width: 110, render: (_, r) => ROLE[r.role] ?? r.role },
    {
      key: 'time',
      title: 'Giờ vào – ra',
      width: 140,
      render: (_, r) => {
        const t = r.session.teacherAttendance?.find((x) => x.teacherId === me);
        return (
          <span className="tabular">
            {t?.checkIn}–{t?.checkOut}
            {r.late && <span className="ml-1 text-xs text-warning-700">muộn</span>}
          </span>
        );
      },
    },
    { key: 'hours', title: 'Giờ', width: 70, align: 'right', render: (_, r) => <span className="tabular">{(r.minutes / 60).toLocaleString('vi-VN', { maximumFractionDigits: 1 })}</span> },
    { key: 'amount', title: 'Thành tiền', width: 130, align: 'right', render: (_, r) => <span className="tabular">{formatCurrency(r.amount + r.bonus)}</span> },
    {
      key: 'confirmed',
      title: 'Xác nhận',
      width: 130,
      mobile: 'aside',
      render: (_, r) =>
        r.confirmed ? (
          <StatusTag tone="success" icon={CalendarCheck} size="sm">
            Đã xác nhận
          </StatusTag>
        ) : (
          <StatusTag tone="warning" icon={Hourglass} size="sm">
            Chờ giáo vụ
          </StatusTag>
        ),
    },
  ];

  return (
    <>
      <PageHeader title="Chấm công" subtitle="Lương dạy = giờ thực dạy (check-in → check-out) × đơn giá. Chỉ buổi giáo vụ đã XÁC NHẬN mới vào lương. Số liệu là tạm tính." />
      <Select value={month} onChange={setMonth} options={monthOptions()} showSearch={false} allowClear={false} className="mb-4 !w-44" />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Buổi đã dạy" value={pay?.items.length ?? 0} icon={CalendarCheck} tone="primary" hint={`${pay?.confirmed.length ?? 0} đã xác nhận`} />
        <StatCard label="Giờ dạy (đã xác nhận)" value={(pay?.hours ?? 0).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} icon={Clock} tone="info" />
        <StatCard label="Lương tạm tính" value={formatCurrency(pay?.total ?? 0)} icon={Wallet} tone="success" hint={pay ? `Dạy ${formatCurrency(pay.teaching)}${pay.substituteBonus ? ` + dạy thay ${formatCurrency(pay.substituteBonus)}` : ''}${pay.lateFine ? ` − muộn ${formatCurrency(pay.lateFine)}` : ''}` : undefined} />
        <StatCard label="Thiếu chấm công" value={missing.length} icon={TriangleAlert} tone={missing.length ? 'danger' : 'neutral'} hint="Báo giáo vụ chấm bù" />
      </div>

      {missing.length > 0 && (
        <div className="mb-4 rounded-xl border border-danger-100 bg-danger-50 p-3 text-[13px] text-danger-700">
          <p className="mb-1 font-semibold">Buổi thiếu check-in / check-out — chưa được tính lương:</p>
          <ul className="flex flex-wrap gap-2">
            {missing.map((s) => (
              <li key={s.id} className="rounded bg-surface px-2 py-0.5">
                {dayjs(s.date).format('DD/MM')} {s.startTime} · {shortCode(classes.find((c) => c.id === s.classId)!)}
              </li>
            ))}
          </ul>
        </div>
      )}

      <DataTable<Item> storageKey="teacher-timesheet" columns={columns} data={pay?.items ?? []} rowKey={(r) => r.session.id} unitLabel="buổi" empty={{ icon: CalendarCheck, title: 'Chưa có buổi dạy trong tháng' }} />

      {pay && (pay.adjusts.length > 0 || pay.base > 0) && (
        <section className="mt-4 rounded-xl border border-line bg-surface p-4 text-[13px]">
          <p className="mb-2 font-semibold text-ink">Khoản khác</p>
          <ul className="flex flex-col gap-1">
            {pay.base > 0 && (
              <li className="flex justify-between">
                <span>Lương cứng</span>
                <span className="tabular">{formatCurrency(pay.base)}</span>
              </li>
            )}
            {pay.adjusts.map((a) => (
              <li key={a.id} className="flex justify-between">
                <span>{a.reason}</span>
                <span className="tabular">{formatCurrency(a.amount)}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  );
}
