import { useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { FileWarning, GraduationCap, KeyRound, Star, UserCheck, UserPlus, Users } from 'lucide-react';
import { Button, DataTable, DrawerForm, PageHeader, Select, StatCard, StatusTag, UserCell, useNotify, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { Q_LEVELS } from '@/features/academic/questions/constants';
import { CONTRACT_TYPES, DEPARTMENTS, EMPLOYEE_STATUS, EMPLOYEE_STATUS_OPTIONS } from '@/features/hr/constants';
import { EmployeeDrawer } from '@/features/hr/EmployeeDrawer';
import { EmployeeFormFields, employeeToForm, formToEmployee, type EmployeeFormValues } from '@/features/hr/EmployeeFormFields';
import * as hrApi from '@/features/hr/hrApi';
import { useHrStore } from '@/features/hr/hrStore';
import { contractEndsIn, toMin } from '@/features/hr/hrUtils';
import type { Department, Employee, EmployeeKind } from '@/features/hr/types';
import { useAuth } from '@/hooks/useAuth';
import { useOpenParam } from '@/hooks/useOpenParam';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { USE_API } from '@/lib/api/config';
import { ALL_BRANCHES } from '@/types/auth';

interface Row extends Employee {
  ends?: number;
  classCount: number;
  hours: number;
  rating?: number;
}

type Dialog = { kind: 'create'; as: EmployeeKind } | { kind: 'edit'; id: string } | null;

/**
 * Nhân viên (mode = staff) / Giáo viên & trợ giảng (mode = teacher).
 * Chế độ API: hồ sơ, hợp đồng, trạng thái lấy từ backend (chỉ hồ sơ trong phạm vi quyền); lớp, giờ dạy, đánh giá vẫn là dữ liệu giả.
 */
export function PeoplePage({ mode }: { mode: 'staff' | 'teacher' }) {
  const { branchId, currentBranch, canDo } = useAuth();
  const notify = useNotify();
  const employees = useHrStore((s) => s.employees);
  const add = useHrStore((s) => s.addEmployee);
  const update = useHrStore((s) => s.updateEmployee);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const reviews = useClassStore((s) => s.teacherReviews);
  const page = mode === 'staff' ? 'hr.employees' : 'hr.teachers';
  const editable = canDo(page, 'update');
  const creatable = canDo(page, 'create');
  /** Hồ sơ server cho xem ở trang này (phạm vi quyền) — null = dùng toàn bộ store */
  const visible = hrApi.useHrVisibility((v) => (USE_API ? v[mode] : null));

  const [group, setGroup] = useState<string>('all');
  const [status, setStatus] = useState<string>();
  const [special, setSpecial] = useState<'ending' | 'noAccount' | undefined>();
  const [level, setLevel] = useState<string>();
  const [showQuit, setShowQuit] = useState(false);
  const [openId, setOpenId] = useState<string | null>(null);
  useOpenParam((id) => {
    if (employees.find((e) => e.id === id)?.status === 'quit') setShowQuit(true);
    setOpenId(id);
  });
  const [dialog, setDialog] = useState<Dialog>(null);

  const monthStart = dayjs().startOf('month').format('YYYY-MM-DD');
  const rows: Row[] = useMemo(
    () =>
      employees
        .filter((e) => (mode === 'staff' ? e.kind === 'staff' : e.kind !== 'staff') && (branchId === ALL_BRANCHES || e.branchId === branchId))
        .filter((e) => !visible || visible.has(e.id))
        .map((e) => {
          let minutes = 0;
          if (e.kind !== 'staff')
            sessions.forEach((s) => {
              if (s.date < monthStart) return;
              const t = s.teacherAttendance?.find((x) => x.teacherId === e.id && x.checkIn && x.checkOut);
              if (t) minutes += Math.max(0, toMin(t.checkOut!) - toMin(t.checkIn!));
            });
          const rv = reviews.filter((r) => r.teacherId === e.id);
          return {
            ...e,
            ends: contractEndsIn(e),
            classCount: classes.filter((c) => c.status === 'ongoing' && (c.teacherIds.includes(e.id) || c.assistantIds?.includes(e.id))).length,
            hours: minutes / 60,
            rating: rv.length ? rv.reduce((n, r) => n + r.overall, 0) / rv.length : undefined,
          };
        }),
    [employees, mode, branchId, classes, sessions, reviews, monthStart, visible],
  );

  const groupOf = (r: Row) => (mode === 'staff' ? r.department : r.kind === 'assistant' ? 'assistant' : r.teacher?.type === 'jp' ? 'jp' : 'vn');
  const base = rows.filter(
    (r) =>
      (showQuit || r.status !== 'quit') &&
      (!status || r.status === status) &&
      (!level || r.teacher?.levels.includes(level as never)) &&
      (special !== 'ending' || (r.ends !== undefined && r.ends <= 45 && r.status !== 'quit')) &&
      (special !== 'noAccount' || (!r.hasAccount && r.status !== 'quit')),
  );
  const counts: Record<string, number> = { all: base.length };
  base.forEach((r) => (counts[groupOf(r)] = (counts[groupOf(r)] ?? 0) + 1));
  const data = group === 'all' ? base : base.filter((r) => groupOf(r) === group);
  const groups: { value: string; label: string }[] =
    mode === 'staff'
      ? (Object.keys(DEPARTMENTS) as Department[]).filter((d) => d !== 'teaching').map((d) => ({ value: d, label: DEPARTMENTS[d] }))
      : [
          { value: 'vn', label: 'GV Việt' },
          { value: 'jp', label: 'GV người Nhật' },
          { value: 'assistant', label: 'Trợ giảng' },
        ];

  const live = rows.filter((r) => r.status !== 'quit');
  const stats = {
    active: live.length,
    probation: live.filter((r) => r.status === 'probation').length,
    ending: live.filter((r) => r.ends !== undefined && r.ends <= 45).length,
    noAccount: live.filter((r) => !r.hasAccount).length,
    hours: live.reduce((n, r) => n + r.hours, 0),
  };

  const contractCell = (r: Row) => (
    <div className="leading-5">
      <p className="text-[13px] text-ink">{CONTRACT_TYPES[r.contract.type]}</p>
      <p className={cn('text-xs', r.ends !== undefined && r.ends <= 45 && r.status !== 'quit' ? 'font-medium text-warning-700' : 'text-ink-muted')}>
        {r.contract.end ? `đến ${fmtDate(r.contract.end)}${r.ends !== undefined && r.ends >= 0 && r.ends <= 45 ? ` (còn ${r.ends} ngày)` : r.ends !== undefined && r.ends < 0 ? ' (hết hạn)' : ''}` : 'Không thời hạn'}
      </p>
    </div>
  );
  const statusCell = (r: Row) => (
    <StatusTag tone={EMPLOYEE_STATUS[r.status].tone} icon={EMPLOYEE_STATUS[r.status].icon} size="sm">
      {EMPLOYEE_STATUS[r.status].label}
    </StatusTag>
  );

  const columns: DataColumn<Row>[] =
    mode === 'staff'
      ? [
          { key: 'fullName', title: 'Nhân viên', width: 230, mobile: 'title', sorter: (a, b) => a.fullName.localeCompare(b.fullName, 'vi'), render: (_, r) => <UserCell size="sm" name={r.fullName} subtitle={`${r.code} · ${r.position}`} /> },
          { key: 'branch', title: 'Chi nhánh', width: 110, mobile: 'subtitle', render: (_, r) => getBranches(false).find((b) => b.id === r.branchId)?.name },
          { key: 'dept', title: 'Phòng ban', width: 150, render: (_, r) => DEPARTMENTS[r.department] },
          { key: 'contract', title: 'Hợp đồng', width: 190, sorter: (a, b) => (a.ends ?? 9999) - (b.ends ?? 9999), render: (_, r) => contractCell(r) },
          { key: 'shift', title: 'Giờ làm', width: 110, render: (_, r) => (r.shiftBased ? <span className="text-primary-700">Theo ca</span> : <span className="text-ink-secondary">Hành chính</span>) },
          { key: 'leave', title: 'Phép còn', width: 90, align: 'right', render: (_, r) => <span className="tabular">{r.leaveBalance} ngày</span> },
          { key: 'account', title: 'Tài khoản', width: 110, render: (_, r) => (r.hasAccount ? <span className="text-success-700">Đã cấp</span> : <span className="text-warning-700">Chưa cấp</span>) },
          { key: 'status', title: 'Trạng thái', width: 140, mobile: 'aside', render: (_, r) => statusCell(r) },
        ]
      : [
          { key: 'fullName', title: 'Giáo viên', width: 230, mobile: 'title', sorter: (a, b) => a.fullName.localeCompare(b.fullName, 'vi'), render: (_, r) => <UserCell size="sm" name={r.fullName} subtitle={`${r.code} · ${r.kind === 'assistant' ? 'Trợ giảng' : r.teacher?.type === 'jp' ? 'GV người Nhật' : 'GV Việt'}`} /> },
          { key: 'branch', title: 'Chi nhánh', width: 100, mobile: 'subtitle', render: (_, r) => getBranches(false).find((b) => b.id === r.branchId)?.name },
          {
            key: 'levels',
            title: 'Dạy cấp',
            width: 150,
            render: (_, r) => (
              <span className="flex flex-wrap gap-1">
                {r.teacher?.levels.map((l) => (
                  <span key={l} className="rounded bg-primary-50 px-1.5 py-0.5 text-[11px] font-semibold text-primary-700">
                    {l}
                  </span>
                ))}
              </span>
            ),
          },
          { key: 'cert', title: 'Chứng chỉ', width: 190, render: (_, r) => <span className="line-clamp-2 text-xs text-ink-secondary">{r.teacher?.certificates.join(' · ')}</span> },
          { key: 'rate', title: 'Đơn giá / giờ', width: 140, align: 'right', render: (_, r) => (r.salaryHidden ? <span className="text-ink-muted">—</span> : <div className="tabular leading-5"><p>{formatCurrency(r.teacher?.rate.offline ?? 0)}</p><p className="text-xs text-ink-muted">online {formatCurrency(r.teacher?.rate.online ?? 0)}</p></div>) },
          { key: 'classes', title: 'Lớp đang dạy', width: 100, align: 'center', sorter: (a, b) => a.classCount - b.classCount, render: (_, r) => <span className="tabular">{r.classCount || '—'}</span> },
          { key: 'hours', title: `Giờ dạy T${dayjs().month() + 1}`, width: 110, align: 'right', sorter: (a, b) => a.hours - b.hours, render: (_, r) => <span className="tabular">{r.hours ? `${r.hours.toFixed(1)}h` : '—'}</span> },
          { key: 'rating', title: 'Đánh giá', width: 90, sorter: (a, b) => (a.rating ?? 0) - (b.rating ?? 0), render: (_, r) => (r.rating ? <span className="tabular inline-flex items-center gap-1"><Star size={13} className="fill-warning-600 text-warning-600" aria-hidden />{r.rating.toFixed(1)}</span> : '—') },
          { key: 'contract', title: 'Hợp đồng', width: 180, render: (_, r) => contractCell(r) },
          { key: 'status', title: 'Trạng thái', width: 130, mobile: 'aside', render: (_, r) => statusCell(r) },
        ];

  const editing = dialog?.kind === 'edit' ? employees.find((e) => e.id === dialog.id) : undefined;
  const createAs: EmployeeKind = dialog?.kind === 'create' ? dialog.as : (editing?.kind ?? 'staff');
  const NEW: Partial<EmployeeFormValues> = {
    gender: 'female',
    branchId: branchId === ALL_BRANCHES ? 'cg' : branchId,
    department: mode === 'staff' ? 'sales' : 'teaching',
    status: 'probation',
    shiftBased: false,
    contractType: 'probation',
    contractStart: dayjs(),
    contractEnd: dayjs().add(2, 'month'),
    teacherType: 'vn',
    levels: [],
    position: createAs === 'assistant' ? 'Trợ giảng' : createAs === 'teacher' ? 'Giáo viên' : undefined,
  };

  return (
    <>
      <PageHeader
        title={mode === 'staff' ? 'Nhân viên' : 'Giáo viên'}
        subtitle={`${mode === 'staff' ? 'Hồ sơ, hợp đồng, phòng ban, làm theo ca' : 'Giáo viên & trợ giảng: cấp độ dạy, chứng chỉ, đơn giá, lịch rảnh, giờ dạy'} · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Nhân sự' }, { title: mode === 'staff' ? 'Nhân viên' : 'Giáo viên' }]}
        actions={
          creatable && (
            <>
              {mode === 'teacher' && (
                <Button icon={UserPlus} variant="outline" collapseOnMobile onClick={() => setDialog({ kind: 'create', as: 'assistant' })}>
                  Thêm trợ giảng
                </Button>
              )}
              <Button action="create" collapseOnMobile onClick={() => setDialog({ kind: 'create', as: mode === 'staff' ? 'staff' : 'teacher' })}>
                {mode === 'staff' ? 'Thêm nhân viên' : 'Thêm giáo viên'}
              </Button>
            </>
          )
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label={mode === 'staff' ? 'Đang làm việc' : 'Giáo viên & trợ giảng'} value={stats.active} icon={mode === 'staff' ? Users : GraduationCap} hint={`${stats.probation} đang thử việc`} />
        <StatCard label="HĐ hết hạn ≤ 45 ngày" value={stats.ending} icon={FileWarning} tone={stats.ending ? 'warning' : 'success'} onClick={() => setSpecial('ending')} />
        {mode === 'staff' ? (
          <StatCard label="Chưa có tài khoản" value={stats.noAccount} icon={KeyRound} tone={stats.noAccount ? 'danger' : 'success'} onClick={() => setSpecial('noAccount')} />
        ) : (
          <StatCard label={`Tổng giờ dạy T${dayjs().month() + 1}`} value={`${stats.hours.toFixed(0)}h`} icon={UserCheck} tone="info" hint="Từ chấm công buổi học" />
        )}
        <StatCard label="Thử việc" value={stats.probation} icon={UserCheck} tone="info" onClick={() => setStatus('probation')} />
      </div>

      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden">
        {[{ value: 'all', label: 'Tất cả' }, ...groups].map((o) => (
          <button
            key={o.value}
            type="button"
            onClick={() => setGroup(o.value)}
            className={cn('inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium', group === o.value ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:bg-primary-50')}
          >
            {o.label}
            <span className={cn('tabular rounded-full px-1.5 text-xs', group === o.value ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[o.value] ?? 0}</span>
          </button>
        ))}
        {special && (
          <button type="button" onClick={() => setSpecial(undefined)} className="inline-flex h-9 shrink-0 items-center rounded-full border border-warning-600 bg-warning-50 px-3.5 text-sm font-medium text-warning-700">
            {special === 'ending' ? 'HĐ sắp hết hạn' : 'Chưa có tài khoản'} ✕
          </button>
        )}
      </div>

      <DataTable<Row>
        storageKey={`hr-${mode}`}
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel={mode === 'staff' ? 'nhân viên' : 'người'}
        searchKeys={['fullName', 'code', 'phone', 'email', 'position']}
        onRowClick={(r) => setOpenId(r.id)}
        search={{
          placeholder: 'Tìm theo tên, mã, SĐT, email, vị trí...',
          activeFilterCount: [status, level, showQuit || undefined].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Trạng thái" value={status} onChange={setStatus} options={EMPLOYEE_STATUS_OPTIONS} showSearch={false} className="lg:!w-40" />
              {mode === 'teacher' && <Select placeholder="Dạy cấp" value={level} onChange={setLevel} options={Q_LEVELS.map((l) => ({ value: l, label: l }))} showSearch={false} className="lg:!w-28" />}
              <label className="inline-flex items-center gap-1.5 text-[13px] whitespace-nowrap text-ink-secondary">
                <input type="checkbox" checked={showQuit} onChange={(e) => setShowQuit(e.target.checked)} /> Hiện người đã nghỉ
              </label>
            </>
          ),
          onReset: () => {
            setStatus(undefined);
            setLevel(undefined);
            setShowQuit(false);
            setSpecial(undefined);
            setGroup('all');
          },
        }}
        rowActions={editable ? (r) => <Button action="edit" iconOnly size="sm" onClick={() => setDialog({ kind: 'edit', id: r.id })} /> : undefined}
        actionsWidth={70}
      />

      {openId && <EmployeeDrawer employeeId={openId} editable={editable} onClose={() => setOpenId(null)} onEdit={(id) => setDialog({ kind: 'edit', id })} />}

      <DrawerForm<EmployeeFormValues>
        key={editing ? `edit-${editing.id}` : `create-${createAs}`}
        open={Boolean(dialog)}
        onClose={() => setDialog(null)}
        width={760}
        columns={2}
        title={editing ? `Sửa hồ sơ ${editing.fullName}` : createAs === 'staff' ? 'Thêm nhân viên' : createAs === 'teacher' ? 'Thêm giáo viên' : 'Thêm trợ giảng'}
        description={editing ? editing.code : 'Nhân viên mới nên tạo qua Tuyển dụng → Onboarding để có đủ checklist nhận việc.'}
        submitAction={editing ? 'save' : 'create'}
        submitText={editing ? 'Lưu' : 'Tạo hồ sơ'}
        successMessage={editing ? 'Đã lưu hồ sơ' : 'Đã tạo hồ sơ'}
        initialValues={editing ? employeeToForm(editing) : NEW}
        onSubmit={async (v) => {
          if (USE_API) {
            // Server kiểm trùng email, vòng quản lý, chi nhánh được gán, sửa đè
            if (editing) {
              const warnings = await hrApi.updateEmployee(editing.id, editing.kind, formToEmployee(v, editing.kind, editing));
              if (warnings.length) notify.warning({ title: 'Lưu ý', description: warnings.join(' ') });
            } else {
              const e = await hrApi.createEmployee(formToEmployee(v, createAs));
              notify.info({ title: `Đã tạo ${e.code}`, description: 'Nhớ cấp tài khoản đăng nhập ở Hệ thống → Phân quyền người dùng.' });
            }
            return;
          }
          if (editing) update(editing.id, formToEmployee(v, editing.kind, editing));
          else {
            const e = add(formToEmployee(v, createAs));
            notify.info({ title: `Đã tạo ${e.code}`, description: 'Nhớ cấp tài khoản đăng nhập ở Hệ thống → Phân quyền người dùng.' });
          }
        }}
      >
        <EmployeeFormFields kind={createAs} editingId={editing?.id} />
      </DrawerForm>
    </>
  );
}

export default function EmployeesPage() {
  return <PeoplePage mode="staff" />;
}
