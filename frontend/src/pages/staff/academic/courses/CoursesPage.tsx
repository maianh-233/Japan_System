import { useMemo, useState } from 'react';
import { useClassStore } from '@/features/academic/classes/classStore';
import { isOpenClass } from '@/features/academic/classes/constants';
import { Tooltip } from 'antd';
import { CirclePause, CirclePlay, Copy } from 'lucide-react';
import {
  Button,
  DataTable,
  DrawerForm,
  PageHeader,
  Select,
  StatusTag,
  useConfirm,
  useNotify,
  type DataColumn,
} from '@/components/common';
import {
  CATEGORIES,
  COURSE_STATUS,
  COURSE_STATUS_OPTIONS,
  courseTotalFee,
  feeLabel,
  FORMATS,
  LEVEL_OPTIONS,
  LEVEL_ORDER,
  levelRange,
  STUDY_MODE_OPTIONS,
  STUDY_MODES,
  TEACHER_TYPE_OPTIONS,
  TEACHER_TYPES,
  totalHours,
} from '@/features/academic/courses/constants';
import { CourseFormFields, type CourseFormValues } from '@/features/academic/courses/CourseFormFields';
import { useCourseStore } from '@/features/academic/courses/courseStore';
import { CourseDetailDrawer, type CourseTab } from '@/features/academic/courses/CourseDetailDrawer';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import type { Course, CourseFormat } from '@/features/academic/courses/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { formatCurrency } from '@/lib/format';
import { ALL_BRANCHES } from '@/types/auth';

/** Dòng bảng = khóa + số lớp đang mở (lớp đang học / sắp mở) */
interface Row extends Course {
  openClasses: string[];
  /** Lộ trình mặc định (code + version) */
  defaultCurriculum?: string;
  curriculumCount: number;
}

type DialogState = { kind: 'create' } | { kind: 'duplicate'; course: Course } | null;

const NEW_COURSE: Partial<CourseFormValues> = {
  format: 'group',
  teacherType: 'vn',
  mode: 'offline',
  levelFrom: 'beginner',
  levelTo: 'N5',
  minutesPerSession: 90,
  sessionsPerWeek: 2,
  maxStudents: 20,
  status: 'draft',
};

/** Khóa học — DỮ LIỆU GIẢ (store trong bộ nhớ). */
export default function CoursesPage() {
  const { branchId, currentBranch, branches, can } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const courses = useCourseStore((s) => s.courses);
  const addCourse = useCourseStore((s) => s.addCourse);
  const setStatus = useCourseStore((s) => s.setStatus);
  const classes = useClassStore((s) => s.classes);
  const editable = can('academic.courses');

  const [format, setFormat] = useState<CourseFormat | 'all'>('all');
  const [level, setLevel] = useState<string>();
  const [teacher, setTeacher] = useState<string>();
  const [mode, setMode] = useState<string>();
  const [status, setStatusFilter] = useState<string>();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  const [openTab, setOpenTab] = useState<CourseTab>('info');
  const curricula = useCurriculumStore((s) => s.curricula);
  const curriculumLinks = useCurriculumStore((s) => s.links);
  const linkCurriculum = useCurriculumStore((s) => s.link);

  const open = (id: string, tab: CourseTab = 'info') => {
    setOpenTab(tab);
    setOpenId(id);
  };

  const rows: Row[] = useMemo(
    () =>
      courses.map((c) => ({
        ...c,
        openClasses: classes
          .filter((k) => k.courseId === c.id && (isOpenClass(k)) && (branchId === ALL_BRANCHES || k.branchId === branchId))
          .map((k) => k.code),
        ...(() => {
          const mine = curriculumLinks.filter((l) => l.courseId === c.id);
          const def = curricula.find((x) => x.id === mine.find((l) => l.isDefault)?.curriculumId);
          return { defaultCurriculum: def ? `${def.code} ${def.version}` : undefined, curriculumCount: mine.length };
        })(),
      })),
    [courses, classes, branchId, curricula, curriculumLinks],
  );

  // Lọc trước hình thức → để đếm số trên từng tab
  const base = useMemo(
    () =>
      rows.filter(
        (c) =>
          (branchId === ALL_BRANCHES || c.branchIds.includes(branchId)) &&
          // Trình độ: khóa có dạy qua cấp độ đó (đầu vào ≤ cấp ≤ đầu ra)
          (!level || (LEVEL_ORDER.indexOf(c.levelFrom) <= LEVEL_ORDER.indexOf(level as never) && LEVEL_ORDER.indexOf(level as never) <= LEVEL_ORDER.indexOf(c.levelTo))) &&
          (!teacher || c.teacherType === teacher) &&
          (!mode || c.mode === mode) &&
          (!status || c.status === status),
      ),
    [rows, branchId, level, teacher, mode, status],
  );
  const counts = { all: base.length, group: base.filter((c) => c.format === 'group').length, private: base.filter((c) => c.format === 'private').length };
  const data = format === 'all' ? base : base.filter((c) => c.format === format);

  const toggleStatus = async (c: Row) => {
    if (c.status !== 'active') {
      setStatus(c.id, 'active');
      notify.success({ title: 'Đã mở tuyển sinh', description: `${c.code} — ${c.name}` });
      return;
    }
    const ok = await confirm({
      title: `Ngừng tuyển sinh khóa ${c.name}?`,
      description: 'Có thể mở lại bất cứ lúc nào.',
      consequences: [
        'Không mở lớp mới và không ghi danh mới cho khóa này',
        'Sale không thấy khóa trong danh sách tư vấn',
        c.openClasses.length ? `${c.openClasses.length} lớp đang mở (${c.openClasses.join(', ')}) vẫn học bình thường đến hết khóa` : 'Hiện không có lớp nào đang mở',
      ],
      tone: 'warning',
      confirmAction: 'save',
      confirmText: 'Ngừng tuyển sinh',
    });
    if (!ok) return;
    setStatus(c.id, 'paused');
    notify.success({ title: 'Đã ngừng tuyển sinh', description: `${c.code} — ${c.name}` });
  };

  const columns: DataColumn<Row>[] = [
    {
      key: 'name',
      title: 'Khóa học',
      width: 230,
      mobile: 'title',
      sorter: (a, b) => a.name.localeCompare(b.name, 'vi'),
      render: (_, r) => (
        <div className={cn('min-w-0', r.status === 'paused' && 'opacity-60')}>
          <p className="truncate font-medium text-ink">{r.name}</p>
          <p className="truncate text-xs text-ink-muted">
            <span className="font-mono">{r.code}</span> · {CATEGORIES[r.category]}
          </p>
        </div>
      ),
    },
    {
      key: 'level',
      title: 'Trình độ',
      width: 140,
      mobile: 'subtitle',
      sorter: (a, b) => LEVEL_ORDER.indexOf(a.levelTo) - LEVEL_ORDER.indexOf(b.levelTo),
      render: (_, r) => <span className="whitespace-nowrap">{levelRange(r)}</span>,
    },
    {
      key: 'format',
      title: 'Hình thức · Giáo viên · Học tại',
      width: 250,
      render: (_, r) => {
        const f = FORMATS[r.format];
        const t = TEACHER_TYPES[r.teacherType];
        const m = STUDY_MODES[r.mode];
        return (
          <span className="flex flex-wrap gap-1">
            <StatusTag tone={f.tone} icon={f.icon} size="sm">
              {r.format === 'private' ? f.label : `${f.short} ≤ ${r.maxStudents}`}
            </StatusTag>
            <StatusTag tone={t.tone} icon={t.icon} size="sm">
              {t.short}
            </StatusTag>
            <StatusTag tone={m.tone} icon={m.icon} size="sm">
              {m.label}
            </StatusTag>
          </span>
        );
      },
    },
    {
      key: 'duration',
      title: 'Thời lượng',
      width: 145,
      sorter: (a, b) => totalHours(a) - totalHours(b),
      render: (_, r) => (
        <div className="leading-5">
          <p className="tabular text-ink">
            {r.sessions} buổi × {r.minutesPerSession}′
          </p>
          <p className="tabular text-xs text-ink-muted">
            {totalHours(r)} giờ · {r.sessionsPerWeek} buổi/tuần
          </p>
        </div>
      ),
    },
    {
      key: 'fee',
      title: 'Học phí',
      width: 165,
      align: 'right',
      sorter: (a, b) => courseTotalFee(a) - courseTotalFee(b),
      render: (_, r) => (
        <div className="leading-5">
          <p className="tabular font-medium text-ink">{feeLabel(r)}</p>
          {r.format === 'private' && <p className="tabular text-xs text-ink-muted">Trọn khóa {formatCurrency(courseTotalFee(r))}</p>}
        </div>
      ),
    },
    {
      key: 'curriculum',
      title: 'Lộ trình',
      width: 140,
      render: (_, r) => (
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            open(r.id, 'curriculum');
          }}
          className="text-left leading-5"
        >
          {r.defaultCurriculum ? (
            <>
              <span className="block font-mono text-[13px] text-primary-700 hover:underline">{r.defaultCurriculum}</span>
              {r.curriculumCount > 1 && <span className="text-xs text-ink-muted">+{r.curriculumCount - 1} lộ trình khác</span>}
            </>
          ) : (
            <span className="text-[13px] font-medium text-warning-700 hover:underline">Chưa gắn</span>
          )}
        </button>
      ),
    },
    {
      key: 'classes',
      title: 'Lớp mở',
      width: 95,
      align: 'center',
      sorter: (a, b) => a.openClasses.length - b.openClasses.length,
      render: (_, r) =>
        r.openClasses.length ? (
          <Tooltip title={r.openClasses.join(', ')}>
            <span className="tabular rounded-md bg-primary-50 px-2 py-0.5 text-[13px] font-medium text-primary-700">{r.openClasses.length} lớp</span>
          </Tooltip>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 165,
      mobile: 'aside',
      render: (_, r) => {
        const s = COURSE_STATUS[r.status];
        return (
          <StatusTag tone={s.tone} icon={s.icon} size="sm">
            {s.label}
          </StatusTag>
        );
      },
    },
  ];

  const toInput = (v: CourseFormValues): CourseFormValues => ({
    ...v,
    name: v.name.trim(),
    maxStudents: v.format === 'private' ? undefined : v.maxStudents,
    description: v.description?.trim() || undefined,
  });

  const initialValues: Partial<CourseFormValues> | undefined =
    dialog?.kind === 'duplicate'
        ? { ...dialog.course, code: `${dialog.course.code}-COPY`, name: `${dialog.course.name} (bản sao)`, status: 'draft' }
        : { ...NEW_COURSE, branchIds: currentBranch ? [currentBranch.id] : branches.map((b) => b.id) };

  return (
    <>
      <PageHeader
        title="Khóa học"
        subtitle={`Các khóa trung tâm đang bán · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Đào tạo' }, { title: 'Khóa học' }]}
        actions={
          <>
            <Button action="export" variant="outline" collapseOnMobile onClick={() => notify.toast.info('Xuất Excel (giả lập)')} />
            {editable && (
              <Button action="create" collapseOnMobile onClick={() => setDialog({ kind: 'create' })}>
                Thêm khóa học
              </Button>
            )}
          </>
        }
      />

      {/* Lọc nhanh theo hình thức lớp */}
      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo hình thức lớp">
        {[{ value: 'all', label: 'Tất cả', icon: null }, ...(['group', 'private'] as const).map((k) => ({ value: k, label: FORMATS[k].label, icon: FORMATS[k].icon }))].map((o) => {
          const active = format === o.value;
          const Icon = o.icon;
          return (
            <button
              key={o.value}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setFormat(o.value as CourseFormat | 'all')}
              className={cn(
                'inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
                active ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
              )}
            >
              {Icon && <Icon size={16} aria-hidden />}
              {o.label}
              <span className={cn('tabular rounded-full px-1.5 text-xs', active ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[o.value as keyof typeof counts]}</span>
            </button>
          );
        })}
      </div>

      <DataTable<Row>
        storageKey="academic-courses"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="khóa học"
        searchKeys={['name', 'code', 'description']}
        onRowClick={(r) => open(r.id)}
        search={{
          placeholder: 'Tìm theo tên, mã khóa, mô tả...',
          activeFilterCount: [level, teacher, mode, status].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Trình độ" value={level} onChange={setLevel} options={LEVEL_OPTIONS} showSearch={false} className="lg:!w-32" />
              <Select placeholder="Giáo viên" value={teacher} onChange={setTeacher} options={TEACHER_TYPE_OPTIONS} showSearch={false} className="lg:!w-40" />
              <Select placeholder="Học tại" value={mode} onChange={setMode} options={STUDY_MODE_OPTIONS} showSearch={false} className="lg:!w-28" />
              <Select placeholder="Trạng thái" value={status} onChange={setStatusFilter} options={COURSE_STATUS_OPTIONS} showSearch={false} className="lg:!w-40" />
            </>
          ),
          onReset: () => {
            setLevel(undefined);
            setTeacher(undefined);
            setMode(undefined);
            setStatusFilter(undefined);
            setFormat('all');
          },
        }}
        rowActions={
          editable
            ? (r) => (
                <>
                  <Button action="edit" iconOnly size="sm" onClick={() => open(r.id)} />
                  <Button icon={Copy} iconOnly size="sm" tone="neutral" variant="soft" tooltip="Nhân bản (tạo biến thể: đổi GV, online/offline...)" aria-label="Nhân bản" onClick={() => setDialog({ kind: 'duplicate', course: r })} />
                  <Button
                    icon={r.status === 'active' ? CirclePause : CirclePlay}
                    iconOnly
                    size="sm"
                    tone={r.status === 'active' ? 'neutral' : 'success'}
                    variant="soft"
                    aria-label={r.status === 'active' ? 'Ngừng tuyển sinh' : 'Mở tuyển sinh'}
                    onClick={() => toggleStatus(r)}
                  />
                </>
              )
            : undefined
        }
        actionsWidth={150}
        empty={{
          title: 'Chưa có khóa học',
          description: 'Tạo khóa học trước, sau đó mở lớp theo khóa ở mục Lớp học.',
          action: editable ? <Button action="create" onClick={() => setDialog({ kind: 'create' })}>Thêm khóa học</Button> : undefined,
        }}
      />

      {/* Thêm mới / nhân bản */}
      <DrawerForm<CourseFormValues>
        key={dialog?.kind === 'duplicate' ? `dup-${dialog.course.id}` : 'create'}
        open={Boolean(dialog)}
        onClose={() => setDialog(null)}
        width={760}
        columns={2}
        title={dialog?.kind === 'duplicate' ? 'Nhân bản khóa học' : 'Thêm khóa học'}
        description={
          dialog?.kind === 'duplicate'
            ? `Từ ${dialog.course.code} — đổi giáo viên, online/offline hoặc 1-1 để tạo biến thể mới. Lộ trình được gắn theo.`
            : 'Tạo xong → gắn lộ trình ở tab Lộ trình'
        }
        submitAction="create"
        submitText="Tạo khóa học"
        successMessage="Đã tạo khóa học"
        initialValues={initialValues}
        onSubmit={async (v) => {
          await new Promise((r) => setTimeout(r, 600)); // giả lập gọi API
          const created = addCourse(toInput(v));
          // Nhân bản: gắn luôn các lộ trình của khóa gốc (giữ mặc định)
          if (dialog?.kind === 'duplicate') {
            curriculumLinks
              .filter((l) => l.courseId === dialog.course.id)
              .forEach((l) => linkCurriculum(created.id, l.curriculumId, l.isDefault));
          }
          setTimeout(() => open(created.id, 'curriculum'), 300);
        }}
      >
        <CourseFormFields />
      </DrawerForm>

      <CourseDetailDrawer
        courseId={openId}
        initialTab={openTab}
        editable={editable}
        hasOpenClasses={Boolean(rows.find((r) => r.id === openId)?.openClasses.length)}
        onClose={() => setOpenId(null)}
      />
      {confirmDialog}
    </>
  );
}
