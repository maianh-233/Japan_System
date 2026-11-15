import { useEffect, useMemo, useState } from 'react';
import { useClassStore } from '@/features/academic/classes/classStore';
import { Checkbox, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { Eye, EyeOff } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { Button, DataTable, PageHeader, Select, StatusTag, UserCell, useNotify, type DataColumn } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import type { Student } from '@/features/crm/types';
import { StudentDetailDrawer, type StudentTab } from '@/features/student/components/StudentDetailDrawer';
import { ACTIVE_ENROLLMENT, STUDY_STATUS, STUDY_STATUS_OPTIONS } from '@/features/student/constants';
import { groupByStudent, studyStatusOf, summarizeTuition, useStudentStore } from '@/features/student/studentStore';
import type { AccountStatus, StudyStatus, TuitionStatus } from '@/features/student/types';
import { useHideStudent } from '@/features/student/useHideStudent';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { MOCK_BRANCHES } from '@/mocks/data/branches';
import { ALL_BRANCHES } from '@/types/auth';

/** Một dòng bảng = hồ sơ + dữ liệu suy ra (lớp, trạng thái học, học phí, tài khoản) */
interface Row extends Student {
  study: StudyStatus;
  currentClasses: string[];
  tuitionStatus?: TuitionStatus;
  debt: number;
  accountStatus?: AccountStatus;
}

const TUITION_FILTER = [
  { value: 'overdue', label: 'Quá hạn' },
  { value: 'debt', label: 'Còn nợ' },
  { value: 'clear', label: 'Đã đóng đủ' },
];
const ACCOUNT_FILTER = [
  { value: 'active', label: 'Đang hoạt động' },
  { value: 'locked', label: 'Bị khóa' },
  { value: 'none', label: 'Chưa cấp' },
];

/** Hồ sơ học viên — DỮ LIỆU GIẢ (store trong bộ nhớ). */
export default function StudentsPage() {
  const { branchId, currentBranch } = useAuth();
  const notify = useNotify();
  const [params, setParams] = useSearchParams();
  const students = useCrmStore((s) => s.students);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const classes = useClassStore((s) => s.classes);
  const accounts = useStudentStore((s) => s.accounts);
  const { toggleHidden, hideBlockReason, confirmDialog } = useHideStudent();

  const [study, setStudy] = useState<StudyStatus | 'all'>('all');
  const [tuition, setTuition] = useState<string>();
  const [account, setAccount] = useState<string>();
  const [showHidden, setShowHidden] = useState(false);
  const [openId, setOpenId] = useState<string | null>(null);
  const [openTab, setOpenTab] = useState<StudentTab>('info');

  const open = (id: string, tab: StudentTab = 'info') => {
    setOpenTab(tab);
    setOpenId(id);
  };

  // Mở thẳng hồ sơ từ nơi khác: /admin/students/profiles?open=ST-1
  useEffect(() => {
    const id = params.get('open');
    if (!id) return;
    const st = students.find((s) => s.id === id);
    if (st?.hidden) setShowHidden(true);
    open(id);
    params.delete('open');
    setParams(params, { replace: true });
  }, [params, setParams, students]);

  const rows: Row[] = useMemo(() => {
    const { enr, inv } = groupByStudent(enrollments, invoices);
    const classCode = new Map(classes.map((c) => [c.id, c.code]));
    const acc = new Map(accounts.filter((a) => a.ownerType === 'student').map((a) => [a.ownerId, a.status]));
    return students.map((s) => {
      const list = enr.get(s.id) ?? [];
      const t = summarizeTuition(inv.get(s.id) ?? []);
      return {
        ...s,
        study: studyStatusOf(list),
        currentClasses: list.filter((e) => ACTIVE_ENROLLMENT.includes(e.status)).map((e) => `${classCode.get(e.classId)}${e.status === 'reserved' ? ' (BL)' : ''}`),
        tuitionStatus: t.status,
        debt: t.remaining,
        accountStatus: acc.get(s.id),
      };
    });
  }, [students, enrollments, invoices, classes, accounts]);

  // Lọc TRƯỚC trạng thái học → để đếm số theo từng trạng thái
  const base = useMemo(
    () =>
      rows.filter(
        (r) =>
          (branchId === ALL_BRANCHES || r.branchId === branchId) &&
          (showHidden || !r.hidden) &&
          (!tuition || (tuition === 'overdue' ? r.tuitionStatus === 'overdue' : tuition === 'debt' ? r.debt > 0 : r.debt === 0)) &&
          (!account || (account === 'none' ? !r.accountStatus : r.accountStatus === account)),
      ),
    [rows, branchId, showHidden, tuition, account],
  );
  const counts = useMemo(() => {
    const c: Record<string, number> = { all: base.length };
    base.forEach((r) => (c[r.study] = (c[r.study] ?? 0) + 1));
    return c;
  }, [base]);
  const data = study === 'all' ? base : base.filter((r) => r.study === study);

  const columns: DataColumn<Row>[] = [
    {
      key: 'fullName',
      title: 'Học viên',
      width: 230,
      mobile: 'title',
      sorter: (a, b) => a.fullName.localeCompare(b.fullName, 'vi'),
      render: (_, r) => (
        <div className={cn(r.hidden && 'opacity-60')}>
          <UserCell name={r.fullName} subtitle={r.code} size="sm" />
        </div>
      ),
    },
    { key: 'phone', title: 'Điện thoại', width: 130, mobile: 'subtitle', render: (_, r) => r.phone ?? <span className="text-ink-muted">—</span> },
    {
      key: 'dob',
      title: 'Ngày sinh',
      width: 125,
      sorter: (a, b) => dayjs(a.dob ?? 0).valueOf() - dayjs(b.dob ?? 0).valueOf(),
      render: (_, r) => (r.dob ? <span className="tabular">{fmtDate(r.dob)}</span> : '—'),
    },
    { key: 'branch', title: 'Chi nhánh', width: 110, render: (_, r) => MOCK_BRANCHES.find((b) => b.id === r.branchId)?.name },
    {
      key: 'classes',
      title: 'Lớp đang học',
      width: 170,
      render: (_, r) =>
        r.currentClasses.length ? (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              open(r.id, 'classes');
            }}
            className="flex flex-wrap gap-1 text-left"
          >
            {r.currentClasses.map((c) => (
              <span key={c} className="rounded-md bg-primary-50 px-1.5 py-0.5 text-[13px] font-medium text-primary-700 hover:bg-primary-100">
                {c}
              </span>
            ))}
          </button>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
    {
      key: 'study',
      title: 'Trạng thái',
      width: 160,
      mobile: 'aside',
      render: (_, r) => {
        const st = STUDY_STATUS[r.study];
        return (
          <span className="inline-flex flex-wrap items-center gap-1">
            <StatusTag tone={st.tone} icon={st.icon} size="sm">
              {st.label}
            </StatusTag>
            {r.hidden && (
              <StatusTag tone="neutral" icon={EyeOff} size="sm">
                Đã ẩn
              </StatusTag>
            )}
          </span>
        );
      },
    },
    {
      key: 'tuition',
      title: 'Học phí',
      width: 140,
      render: (_, r) => (r.tuitionStatus ? <StatusTag group="invoice" status={r.tuitionStatus} size="sm" /> : <span className="text-ink-muted">—</span>),
    },
    {
      key: 'debt',
      title: 'Còn nợ',
      width: 125,
      align: 'right',
      sorter: (a, b) => a.debt - b.debt,
      render: (_, r) => (r.debt ? <span className="tabular font-medium text-danger-600">{formatCurrency(r.debt)}</span> : <span className="text-ink-muted">—</span>),
    },
    {
      key: 'account',
      title: 'Tài khoản',
      width: 150,
      render: (_, r) => (r.accountStatus ? <StatusTag group="account" status={r.accountStatus} size="sm" /> : <span className="text-ink-muted">Chưa cấp</span>),
    },
  ];

  return (
    <>
      <PageHeader
        title="Hồ sơ học viên"
        subtitle={`Học viên đã đăng ký học · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Học viên' }, { title: 'Hồ sơ học viên' }]}
        actions={<Button action="export" variant="outline" collapseOnMobile onClick={() => notify.toast.info('Xuất Excel (giả lập)')} />}
      />

      {/* Lọc nhanh theo trạng thái học */}
      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo trạng thái học">
        {[{ value: 'all', label: 'Tất cả' } as const, ...STUDY_STATUS_OPTIONS].map((o) => {
          const active = study === o.value;
          return (
            <button
              key={o.value}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setStudy(o.value)}
              className={cn(
                'inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
                active ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
              )}
            >
              {o.label}
              <span className={cn('tabular rounded-full px-1.5 text-xs', active ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[o.value] ?? 0}</span>
            </button>
          );
        })}
      </div>

      <DataTable<Row>
        storageKey="students-profiles"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="học viên"
        searchKeys={['fullName', 'code', 'phone', 'email']}
        onRowClick={(r) => open(r.id)}
        search={{
          placeholder: 'Tìm theo tên, mã HV, SĐT, email...',
          activeFilterCount: [tuition, account, showHidden || undefined].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Học phí" value={tuition} onChange={setTuition} options={TUITION_FILTER} showSearch={false} className="lg:!w-40" />
              <Select placeholder="Tài khoản" value={account} onChange={setAccount} options={ACCOUNT_FILTER} showSearch={false} className="lg:!w-40" />
              <Checkbox checked={showHidden} onChange={(e) => setShowHidden(e.target.checked)} className="whitespace-nowrap">
                Hiện học viên đã ẩn
              </Checkbox>
            </>
          ),
          onReset: () => {
            setTuition(undefined);
            setAccount(undefined);
            setShowHidden(false);
            setStudy('all');
          },
        }}
        rowActions={(r) => {
          const blocked = r.hidden ? null : hideBlockReason(r.id);
          return (
            <>
              <Button action="edit" iconOnly size="sm" tooltip="Xem / sửa" onClick={() => open(r.id)} />
              <Tooltip title={blocked ?? (r.hidden ? 'Bỏ ẩn học viên' : 'Ẩn học viên')}>
                <span>
                  <Button
                    icon={r.hidden ? Eye : EyeOff}
                    iconOnly
                    size="sm"
                    tone="neutral"
                    variant="soft"
                    disabled={Boolean(blocked)}
                    aria-label={r.hidden ? 'Bỏ ẩn học viên' : 'Ẩn học viên'}
                    tooltip={false}
                    onClick={() => toggleHidden(r)}
                  />
                </span>
              </Tooltip>
            </>
          );
        }}
        actionsWidth={110}
        empty={{
          title: 'Chưa có học viên',
          description: 'Hồ sơ học viên được tạo từ Lead khi khách đăng ký học (Tuyển sinh → Lead → tab Học viên).',
        }}
      />

      <StudentDetailDrawer studentId={openId} initialTab={openTab} onClose={() => setOpenId(null)} />
      {confirmDialog}
    </>
  );
}
