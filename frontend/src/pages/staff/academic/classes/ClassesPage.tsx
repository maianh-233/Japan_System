import { useMemo, useState } from 'react';
import { Form, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { AlertTriangle, MonitorPlay } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DataTable, ModalForm, PageHeader, Select, StatusTag, useNotify, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { ClassFormFields, formToClass, type ClassFormValues } from '@/features/academic/classes/ClassFormFields';
import {
  CLASS_STATUS,
  CLASS_STATUS_OPTIONS,
  CODE_PREFIX,
  hoursPerSession,
  scheduleLabel,
  startDelayDays,
  TEACHERS,
  teacherNames,
  totalSessionsOf,
} from '@/features/academic/classes/constants';
import type { ClassInfo, ClassStatus } from '@/features/academic/classes/types';
import { useCourseStore } from '@/features/academic/courses/courseStore';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { MOCK_BRANCHES } from '@/mocks/data/branches';
import { ALL_BRANCHES } from '@/types/auth';

interface Row extends ClassInfo {
  enrolled: number;
  sessionsDone: number;
  sessionsTotal: number;
}

/** Lớp học — DỮ LIỆU GIẢ (store trong bộ nhớ). Bấm Sửa / dòng → trang quản lý lớp. */
export default function ClassesPage() {
  const navigate = useNavigate();
  const notify = useNotify();
  const { branchId, currentBranch, can, branches } = useAuth();
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const addClass = useClassStore((s) => s.addClass);
  const enrollments = useStudentStore((s) => s.enrollments);
  const courses = useCourseStore((s) => s.courses);
  const editable = can('academic.classes');

  const [status, setStatus] = useState<ClassStatus | 'all'>('all');
  const [courseId, setCourseId] = useState<string>();
  const [teacherId, setTeacherId] = useState<string>();
  const [creating, setCreating] = useState(false);

  const rows: Row[] = useMemo(
    () =>
      classes.map((c) => {
        const mine = sessions.filter((s) => s.classId === c.id);
        return {
          ...c,
          enrolled: enrollments.filter((e) => e.classId === c.id && (e.status === 'studying' || e.status === 'reserved')).length,
          sessionsDone: mine.filter((s) => s.status === 'completed' || s.status === 'makeup').length,
          sessionsTotal: totalSessionsOf(c),
        };
      }),
    [classes, sessions, enrollments],
  );

  const base = useMemo(
    () =>
      rows.filter(
        (r) =>
          (branchId === ALL_BRANCHES || r.branchId === branchId) && (!courseId || r.courseId === courseId) && (!teacherId || r.teacherIds.includes(teacherId)),
      ),
    [rows, branchId, courseId, teacherId],
  );
  const counts = useMemo(() => {
    const c: Record<string, number> = { all: base.length };
    base.forEach((r) => (c[r.status] = (c[r.status] ?? 0) + 1));
    return c;
  }, [base]);
  const data = status === 'all' ? base : base.filter((r) => r.status === status);
  const open = (id: string) => navigate(`/admin/academic/classes/${id}`);

  const columns: DataColumn<Row>[] = [
    {
      key: 'name',
      title: 'Lớp',
      width: 250,
      mobile: 'title',
      sorter: (a, b) => a.code.localeCompare(b.code),
      render: (_, r) => (
        <div className={cn('min-w-0', r.status === 'cancelled' && 'opacity-60')}>
          <p className="truncate font-mono text-[13px] font-semibold text-primary-700">{r.code}</p>
          <p className="truncate text-[13px] text-ink">{r.name}</p>
        </div>
      ),
    },
    {
      key: 'course',
      title: 'Khóa · Chi nhánh',
      width: 170,
      render: (_, r) => (
        <div className="leading-5">
          <p className="truncate text-ink">{r.courseLabel}</p>
          <p className="truncate text-xs text-ink-muted">
            {MOCK_BRANCHES.find((b) => b.id === r.branchId)?.name}
            {r.online ? ' · Online' : r.room ? ` · ${r.room}` : ''}
          </p>
        </div>
      ),
    },
    { key: 'teacher', title: 'Giáo viên', width: 160, render: (_, r) => <span className="text-[13px]">{teacherNames(r.teacherIds)}</span> },
    {
      key: 'schedule',
      title: 'Lịch học',
      width: 190,
      mobile: 'subtitle',
      render: (_, r) => (
        <span className="inline-flex items-center gap-1 whitespace-nowrap text-[13px]">
          {r.online && <MonitorPlay size={13} className="text-ink-muted" aria-label="Online" />}
          {scheduleLabel(r)}
        </span>
      ),
    },
    {
      key: 'start',
      title: 'Khai giảng',
      width: 185,
      sorter: (a, b) => dayjs(a.actualStartDate ?? a.plannedStartDate).valueOf() - dayjs(b.actualStartDate ?? b.plannedStartDate).valueOf(),
      render: (_, r) => {
        const delay = startDelayDays(r);
        const overdue = r.status === 'enrolling' && dayjs(r.plannedStartDate).isBefore(dayjs(), 'day');
        return (
          <div className="leading-5">
            <p className="tabular text-ink">
              {r.actualStartDate ? fmtDate(r.actualStartDate) : fmtDate(r.plannedStartDate)}
              {!r.actualStartDate && r.status !== 'cancelled' && <span className="text-xs text-ink-muted"> (dự kiến)</span>}
            </p>
            {delay > 0 && <p className="text-xs text-warning-700">Trễ {delay} ngày so với dự kiến</p>}
            {overdue && (
              <p className="inline-flex items-center gap-1 text-xs font-medium text-danger-600">
                <AlertTriangle size={12} aria-hidden /> Quá ngày dự kiến
              </p>
            )}
          </div>
        );
      },
    },
    {
      key: 'end',
      title: 'Kết thúc',
      width: 130,
      render: (_, r) => (
        <p className="tabular leading-5 text-ink">
          {fmtDate(r.actualEndDate ?? r.plannedEndDate)}
          {!r.actualEndDate && r.status !== 'cancelled' && <span className="block text-xs text-ink-muted">(dự kiến)</span>}
        </p>
      ),
    },
    {
      key: 'hours',
      title: 'Thời lượng',
      width: 140,
      render: (_, r) => (
        <div className="leading-5">
          <p className="tabular text-ink">{r.totalHours} giờ</p>
          <p className="tabular text-xs text-ink-muted">
            {r.sessionsTotal} buổi × {hoursPerSession(r).toLocaleString('vi-VN')}h
          </p>
        </div>
      ),
    },
    {
      key: 'students',
      title: 'Sĩ số',
      width: 110,
      align: 'center',
      sorter: (a, b) => a.enrolled - b.enrolled,
      render: (_, r) => {
        const short = r.status === 'enrolling' && r.enrolled < r.minStudents;
        return (
          <Tooltip title={short ? `Cần tối thiểu ${r.minStudents} học viên để khai giảng` : `Tối thiểu ${r.minStudents} · tối đa ${r.maxStudents}`}>
            <span className={cn('tabular rounded-md px-2 py-0.5 text-[13px] font-medium', short ? 'bg-danger-50 text-danger-700' : 'bg-page text-ink')}>
              {r.enrolled}/{r.maxStudents}
            </span>
          </Tooltip>
        );
      },
    },
    {
      key: 'progress',
      title: 'Tiến độ',
      width: 130,
      sorter: (a, b) => a.sessionsDone / (a.sessionsTotal || 1) - b.sessionsDone / (b.sessionsTotal || 1),
      render: (_, r) => {
        const pct = r.sessionsTotal ? Math.round((r.sessionsDone / r.sessionsTotal) * 100) : 0;
        return (
          <div className="flex flex-col gap-1">
            <span className="tabular text-xs text-ink-secondary">
              {r.sessionsDone}/{r.sessionsTotal} buổi
            </span>
            <span className="h-1.5 w-full overflow-hidden rounded-full bg-disabled" aria-hidden>
              <span className="block h-full rounded-full bg-success-600" style={{ width: `${pct}%` }} />
            </span>
          </div>
        );
      },
    },
    {
      key: 'status',
      title: 'Tình trạng',
      width: 150,
      mobile: 'aside',
      render: (_, r) => {
        const s = CLASS_STATUS[r.status];
        return (
          <Tooltip title={r.cancelReason ?? s.hint}>
            <span>
              <StatusTag tone={s.tone} icon={s.icon} size="sm">
                {s.label}
              </StatusTag>
            </span>
          </Tooltip>
        );
      },
    },
  ];

  return (
    <>
      <PageHeader
        title="Lớp học"
        subtitle={`Quản lý lớp theo khóa · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Đào tạo' }, { title: 'Lớp học' }]}
        actions={
          <>
            <Button action="export" variant="outline" collapseOnMobile onClick={() => notify.toast.info('Xuất Excel (giả lập)')} />
            {editable && (
              <Button action="create" collapseOnMobile onClick={() => setCreating(true)}>
                Mở lớp mới
              </Button>
            )}
          </>
        }
      />

      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo tình trạng">
        {[{ value: 'all', label: 'Tất cả' } as const, ...CLASS_STATUS_OPTIONS].map((o) => {
          const active = status === o.value;
          return (
            <button
              key={o.value}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setStatus(o.value)}
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
        storageKey="academic-classes"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="lớp"
        searchKeys={['code', 'name', 'courseLabel']}
        onRowClick={(r) => open(r.id)}
        search={{
          placeholder: 'Tìm theo mã lớp, tên lớp, khóa...',
          activeFilterCount: [courseId, teacherId].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Khóa học" value={courseId} onChange={setCourseId} options={courses.map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` }))} className="lg:!w-56" />
              <Select
                placeholder="Giáo viên"
                value={teacherId}
                onChange={setTeacherId}
                options={TEACHERS.filter((t) => branchId === ALL_BRANCHES || t.branchIds.includes(branchId)).map((t) => ({ value: t.id, label: t.name }))}
                className="lg:!w-44"
              />
            </>
          ),
          onReset: () => {
            setCourseId(undefined);
            setTeacherId(undefined);
            setStatus('all');
          },
        }}
        rowActions={(r) => <Button action={editable ? 'edit' : 'view'} iconOnly size="sm" tooltip="Mở trang quản lý lớp" onClick={() => open(r.id)} />}
        actionsWidth={70}
        empty={{
          title: 'Chưa có lớp',
          description: 'Mở lớp mới theo một khóa học đang tuyển sinh.',
          action: editable ? <Button action="create" onClick={() => setCreating(true)}>Mở lớp mới</Button> : undefined,
        }}
      />

      <ModalForm<ClassFormValues>
        open={creating}
        onClose={() => setCreating(false)}
        columns={2}
        width={820}
        title="Mở lớp mới"
        description="Lớp ở trạng thái Chờ khai giảng — đủ học viên tối thiểu thì bấm Khai giảng trong trang lớp"
        submitAction="create"
        submitText="Mở lớp"
        successMessage="Đã mở lớp"
        initialValues={{ branchId: currentBranch?.id ?? branches[0]?.id, weekdays: [1, 3, 5], startTime: dayjs().hour(18).minute(0), endTime: dayjs().hour(20).minute(0), plannedStartDate: dayjs().add(14, 'day') }}
        onSubmit={async (v) => {
          await new Promise((r) => setTimeout(r, 600)); // giả lập gọi API
          const course = courses.find((c) => c.id === v.courseId)!;
          const created = addClass(formToClass(v, CODE_PREFIX[course.teacherType], course.name, course.mode === 'online'));
          setTimeout(() => navigate(`/admin/academic/classes/${created.id}?tab=sessions`), 200);
        }}
      >
        <Form.Item noStyle>
          <ClassFormFields mode="create" />
        </Form.Item>
      </ModalForm>
    </>
  );
}
