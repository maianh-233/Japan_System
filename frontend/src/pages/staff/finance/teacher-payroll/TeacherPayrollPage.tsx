import { useMemo, useState } from 'react';
import { Drawer } from 'antd';
import dayjs from 'dayjs';
import { CalendarCheck2, Clock, HandCoins, Hourglass, Plus, Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DataTable, PageHeader, StatCard, StatusTag, UserCell, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { ROLE_LABEL } from '@/features/academic/sessions/sessionOps';
import { SUBSTITUTE_BONUS, TEACHER_LATE_FINE } from '@/features/finance/constants';
import { useFinanceStore } from '@/features/finance/financeStore';
import { teacherPayroll, type TeacherPay } from '@/features/finance/financeUtils';
import { AdjustModal, PayrollBar, usePayrollLock } from '@/features/finance/PayrollBar';
import { EMPLOYEE_KIND } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { ALL_BRANCHES } from '@/types/auth';

/** Lương giáo viên / trợ giảng: tự tính từ buổi đã dạy (đã xác nhận) × đơn giá. DỮ LIỆU GIẢ. */
export default function TeacherPayrollPage() {
  const { branchId } = useAuth();
  const { isMobile } = useBreakpoint();
  const navigate = useNavigate();
  const employees = useHrStore((s) => s.employees);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const adjusts = useFinanceStore((s) => s.adjusts);
  const removeAdjust = useFinanceStore((s) => s.removeAdjust);

  const [month, setMonth] = useState(dayjs().format('YYYY-MM'));
  const [openId, setOpenId] = useState<string | null>(null);
  const [adjFor, setAdjFor] = useState<TeacherPay | null>(null);
  const { locked } = usePayrollLock('teacher', month);

  const all = useMemo(() => teacherPayroll(month, employees, classes, sessions, adjusts), [month, employees, classes, sessions, adjusts]);
  const rows = all.filter((p) => branchId === ALL_BRANCHES || p.employee.branchId === branchId).filter((p) => p.items.length || p.base || p.adjust).sort((a, b) => b.total - a.total);
  const totals = rows.reduce<Record<string, number>>((m, p) => ({ ...m, [p.employee.branchId]: (m[p.employee.branchId] ?? 0) + p.total }), {});
  const pending = rows.reduce((n, p) => n + p.pending.length, 0);
  const open = rows.find((p) => p.employee.id === openId);

  const columns: DataColumn<TeacherPay>[] = [
    { key: 'name', title: 'Giáo viên', width: 210, mobile: 'title', render: (_, p) => <UserCell size="sm" name={p.employee.fullName} subtitle={`${EMPLOYEE_KIND[p.employee.kind]}${p.employee.teacher?.type === 'jp' ? ' · người Nhật' : ''}`} /> },
    {
      key: 'sessions',
      title: 'Buổi dạy',
      width: 120,
      align: 'center',
      render: (_, p) => (
        <div className="leading-5">
          <p className="tabular font-medium">{p.confirmed.length}</p>
          {p.pending.length > 0 && <p className="text-[11px] text-warning-700">+{p.pending.length} chờ xác nhận</p>}
        </div>
      ),
    },
    { key: 'hours', title: 'Giờ dạy', width: 90, align: 'right', render: (_, p) => <span className="tabular">{p.hours.toFixed(1)}h</span> },
    {
      key: 'rate',
      title: 'Đơn giá / giờ',
      width: 150,
      render: (_, p) => (
        <span className="text-xs leading-5 text-ink-secondary">
          Off {formatCurrency(p.employee.teacher?.rate.offline)}
          <br />
          On {formatCurrency(p.employee.teacher?.rate.online)}
        </span>
      ),
    },
    { key: 'teaching', title: 'Tiền dạy', width: 130, align: 'right', render: (_, p) => <span className="tabular">{formatCurrency(p.teaching)}</span> },
    {
      key: 'extra',
      title: 'Dạy thay / phạt',
      width: 140,
      align: 'right',
      render: (_, p) => (
        <div className="tabular text-xs leading-5">
          {p.substituteBonus > 0 && <p className="text-success-700">+{formatCurrency(p.substituteBonus)}</p>}
          {p.lateFine > 0 && <p className="text-danger-600">−{formatCurrency(p.lateFine)}</p>}
          {!p.substituteBonus && !p.lateFine && <span className="text-ink-muted">—</span>}
        </div>
      ),
    },
    { key: 'adjust', title: 'Thưởng / phạt', width: 120, align: 'right', render: (_, p) => (p.adjust ? <span className={cn('tabular text-[13px]', p.adjust > 0 ? 'text-success-700' : 'text-danger-600')}>{p.adjust > 0 ? '+' : ''}{formatCurrency(p.adjust)}</span> : <span className="text-ink-muted">—</span>) },
    { key: 'total', title: 'Thực nhận', width: 140, align: 'right', mobile: 'aside', render: (_, p) => <span className="tabular font-semibold text-ink">{formatCurrency(p.total)}</span> },
  ];

  return (
    <>
      <PageHeader
        title="Lương giáo viên"
        subtitle={`Tự tính từ chấm công buổi dạy ĐÃ được giáo vụ xác nhận × đơn giá (offline / online). Buổi hủy không tính · dạy thay +${formatCurrency(SUBSTITUTE_BONUS)}/buổi · đi muộn −${formatCurrency(TEACHER_LATE_FINE)}/buổi.`}
        breadcrumb={[{ title: 'Tài chính' }, { title: 'Lương giáo viên' }]}
      />

      <PayrollBar kind="teacher" month={month} onMonth={setMonth} totals={totals} pendingWarning={pending ? `Còn ${pending} buổi chưa được xác nhận — sẽ không được tính vào tháng này` : undefined} />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Tổng quỹ lương GV" value={formatCurrency(rows.reduce((n, p) => n + p.total, 0))} icon={HandCoins} tone="primary" hint={`${rows.length} người`} />
        <StatCard label="Tổng giờ dạy" value={`${rows.reduce((n, p) => n + p.hours, 0).toFixed(1)}h`} icon={Clock} hint={`${rows.reduce((n, p) => n + p.confirmed.length, 0)} buổi đã xác nhận`} />
        <StatCard label="Buổi chờ xác nhận" value={pending} icon={Hourglass} tone={pending ? 'warning' : 'success'} hint="Giáo vụ xác nhận ở trang Buổi học" onClick={pending ? () => navigate('/admin/academic/sessions') : undefined} />
        <StatCard label="Buổi dạy thay" value={rows.reduce((n, p) => n + p.confirmed.filter((i) => i.role === 'substitute').length, 0)} icon={CalendarCheck2} tone="info" />
      </div>

      <DataTable<TeacherPay>
        storageKey="fin-teacher-pay"
        columns={columns}
        data={rows}
        rowKey={(p) => p.employee.id}
        unitLabel="giáo viên"
        onRowClick={(p) => setOpenId(p.employee.id)}
        rowActions={(p) => (!locked ? <Button icon={Plus} iconOnly size="sm" variant="ghost" tone="neutral" tooltip="Thưởng / phạt" aria-label="Thưởng / phạt" onClick={() => setAdjFor(p)} /> : null)}
        actionsWidth={60}
        empty={{ icon: HandCoins, title: 'Chưa có buổi dạy trong tháng' }}
      />

      {/* Phiếu lương */}
      <Drawer open={Boolean(open)} onClose={() => setOpenId(null)} width={isMobile ? '100%' : 640} destroyOnHidden title={open && `Phiếu lương ${month.slice(5)}/${month.slice(0, 4)} · ${open.employee.fullName}`}>
        {open && (
          <div className="flex flex-col gap-4 text-[13px]">
            <div className="grid grid-cols-2 gap-2 rounded-xl border border-line p-3 sm:grid-cols-3">
              {(
                [
                  ['Lương cứng', open.base],
                  [`Tiền dạy (${open.hours.toFixed(1)}h)`, open.teaching],
                  ['Phụ cấp dạy thay', open.substituteBonus],
                  ['Phạt đi muộn', -open.lateFine],
                  ['Thưởng / phạt khác', open.adjust],
                  ['Thực nhận', open.total],
                ] as [string, number][]
              ).map(([l, v], i) => (
                <div key={l}>
                  <p className="text-xs text-ink-muted">{l}</p>
                  <p className={cn('tabular font-semibold', i === 5 ? 'text-lg text-primary-700' : v < 0 ? 'text-danger-600' : 'text-ink')}>{formatCurrency(v)}</p>
                </div>
              ))}
            </div>

            {open.pending.length > 0 && (
              <p className="rounded-lg bg-warning-50 px-3 py-2 text-warning-700">
                {open.pending.length} buổi chưa được xác nhận ({formatCurrency(open.pending.reduce((n, i) => n + i.amount, 0))}) — chưa tính lương. Xác nhận ở trang Buổi học, nếu chốt sau sẽ chuyển sang tháng sau.
              </p>
            )}
            {open.cancelled > 0 && <p className="text-ink-secondary">{open.cancelled} buổi bị hủy trong tháng — không tính lương.</p>}

            <div>
              <div className="mb-2 flex items-center justify-between">
                <p className="font-semibold text-ink">Thưởng / phạt</p>
                {!locked && (
                  <Button icon={Plus} size="sm" variant="soft" onClick={() => setAdjFor(open)}>
                    Thêm
                  </Button>
                )}
              </div>
              <ul className="flex flex-col gap-1.5">
                {open.adjusts.map((a) => (
                  <li key={a.id} className="flex items-center justify-between gap-2 rounded-lg bg-canvas px-3 py-2">
                    <span>
                      {a.reason} <span className="text-xs text-ink-muted">· {a.by}</span>
                    </span>
                    <span className="flex items-center gap-1">
                      <span className={cn('tabular font-medium', a.amount > 0 ? 'text-success-700' : 'text-danger-600')}>{a.amount > 0 ? '+' : ''}{formatCurrency(a.amount)}</span>
                      {!locked && <Button icon={Trash2} iconOnly size="sm" variant="ghost" tone="danger" aria-label="Xóa" onClick={() => removeAdjust(a.id)} />}
                    </span>
                  </li>
                ))}
                {!open.adjusts.length && <li className="text-ink-muted">Không có</li>}
              </ul>
            </div>

            <div>
              <p className="mb-2 font-semibold text-ink">Chi tiết {open.items.length} buổi</p>
              <div className="overflow-x-auto rounded-lg border border-line">
                <table className="w-full min-w-[520px] text-[13px]">
                  <thead className="bg-canvas text-left text-xs text-ink-muted">
                    <tr>
                      <th className="px-2 py-1.5 font-medium">Ngày</th>
                      <th className="px-2 py-1.5 font-medium">Lớp</th>
                      <th className="px-2 py-1.5 font-medium">Vai trò</th>
                      <th className="px-2 py-1.5 text-right font-medium">Phút</th>
                      <th className="px-2 py-1.5 text-right font-medium">Thành tiền</th>
                    </tr>
                  </thead>
                  <tbody>
                    {open.items.map((i) => (
                      <tr key={i.session.id} className={cn('border-t border-line', !i.confirmed && 'text-ink-muted')}>
                        <td className="px-2 py-1.5">
                          {fmtDate(i.session.date)} <span className="text-xs text-ink-muted">{i.session.startTime}</span>
                        </td>
                        <td className="px-2 py-1.5">
                          {i.cls.code}
                          {i.online && <span className="ml-1 rounded bg-primary-50 px-1 text-[10px] text-primary-700">online</span>}
                        </td>
                        <td className="px-2 py-1.5">
                          {ROLE_LABEL[i.role]}
                          {i.late && <span className="ml-1 text-[11px] text-danger-600">muộn</span>}
                        </td>
                        <td className="tabular px-2 py-1.5 text-right">{i.minutes}</td>
                        <td className="tabular px-2 py-1.5 text-right">
                          {i.confirmed ? formatCurrency(i.amount + i.bonus) : <StatusTag tone="warning" icon={Hourglass} size="sm">Chờ XN</StatusTag>}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}
      </Drawer>

      <AdjustModal open={Boolean(adjFor)} onClose={() => setAdjFor(null)} month={month} employee={adjFor?.employee} />
    </>
  );
}
