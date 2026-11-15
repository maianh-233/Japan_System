import { useMemo, useState } from 'react';
import { Tooltip } from 'antd';
import dayjs from 'dayjs';
import { Copy, Star } from 'lucide-react';
import { Button, DataTable, ModalForm, PageHeader, Select, StatusTag, useNotify, type DataColumn } from '@/components/common';
import { LEVEL_OPTIONS, LEVEL_ORDER, LEVELS } from '@/features/academic/courses/constants';
import { useCourseStore } from '@/features/academic/courses/courseStore';
import { CURRICULUM_STATUS, CURRICULUM_STATUS_OPTIONS, TIER_OPTIONS, TIERS } from '@/features/academic/curriculum/constants';
import { CurriculumDetailDrawer, type CurriculumTab } from '@/features/academic/curriculum/CurriculumDetailDrawer';
import { CurriculumFormFields, type CurriculumFormValues } from '@/features/academic/curriculum/CurriculumFormFields';
import { curriculumStats, nextVersion, useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import type { Curriculum, CurriculumStatus } from '@/features/academic/curriculum/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';

interface Row extends Curriculum {
  stats: ReturnType<typeof curriculumStats>;
  courses: { id: string; code: string; isDefault: boolean }[];
  /** Ghép tag thành chuỗi để ô tìm kiếm tìm được theo tag */
  tagText: string;
}

type Dialog = { kind: 'create' } | { kind: 'clone'; source: Curriculum } | null;

/** Lộ trình (Curriculum) — DỮ LIỆU GIẢ (store trong bộ nhớ). */
export default function CurriculumPage() {
  const { can, user } = useAuth();
  const notify = useNotify();
  const curricula = useCurriculumStore((s) => s.curricula);
  const links = useCurriculumStore((s) => s.links);
  const addCurriculum = useCurriculumStore((s) => s.addCurriculum);
  const clone = useCurriculumStore((s) => s.cloneCurriculum);
  const courses = useCourseStore((s) => s.courses);
  const editable = can('academic.curriculum');

  const [status, setStatus] = useState<CurriculumStatus | 'all'>('all');
  const [level, setLevel] = useState<string>();
  const [tier, setTier] = useState<string>();
  const [tag, setTag] = useState<string>();
  const [courseId, setCourseId] = useState<string>();
  const [dialog, setDialog] = useState<Dialog>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  const [openTab, setOpenTab] = useState<CurriculumTab>('content');

  const open = (id: string, tab: CurriculumTab = 'content') => {
    setOpenTab(tab);
    setOpenId(id);
  };

  const rows: Row[] = useMemo(
    () =>
      curricula.map((c) => ({
        ...c,
        stats: curriculumStats(c),
        courses: links
          .filter((l) => l.curriculumId === c.id)
          .map((l) => ({ id: l.courseId, code: courses.find((x) => x.id === l.courseId)?.code ?? l.courseId, isDefault: l.isDefault }))
          .sort((a, b) => Number(b.isDefault) - Number(a.isDefault)),
        tagText: c.tags.join(' '),
      })),
    [curricula, links, courses],
  );
  const allTags = useMemo(() => [...new Set(curricula.flatMap((c) => c.tags))].sort(), [curricula]);

  const base = useMemo(
    () =>
      rows.filter(
        (r) =>
          (!level || r.level === level) &&
          (!tier || r.tier === tier) &&
          (!tag || r.tags.includes(tag)) &&
          (!courseId || r.courses.some((c) => c.id === courseId)),
      ),
    [rows, level, tier, tag, courseId],
  );
  const counts = useMemo(() => {
    const c: Record<string, number> = { all: base.length };
    base.forEach((r) => (c[r.status] = (c[r.status] ?? 0) + 1));
    return c;
  }, [base]);
  const data = status === 'all' ? base : base.filter((r) => r.status === status);

  const columns: DataColumn<Row>[] = [
    {
      key: 'name',
      title: 'Lộ trình',
      width: 270,
      mobile: 'title',
      sorter: (a, b) => a.name.localeCompare(b.name, 'vi'),
      render: (_, r) => (
        <div className={cn('min-w-0', r.status === 'archived' && 'opacity-60')}>
          <p className="truncate font-medium text-ink" title={r.name}>
            {r.name}
          </p>
          <p className="truncate text-xs text-ink-muted">
            <span className="font-mono">{r.code}</span> · <span className="font-medium text-ink-secondary">{r.version}</span>
            {r.textbook && ` · ${r.textbook}`}
          </p>
        </div>
      ),
    },
    {
      key: 'level',
      title: 'Cấp độ',
      width: 95,
      mobile: 'subtitle',
      sorter: (a, b) => LEVEL_ORDER.indexOf(a.level) - LEVEL_ORDER.indexOf(b.level),
      render: (_, r) => LEVELS[r.level],
    },
    {
      key: 'tier',
      title: 'Bậc',
      width: 120,
      render: (_, r) => {
        const t = TIERS[r.tier];
        const tag = (
          <StatusTag tone={t.tone} icon={t.icon} size="sm">
            {t.label}
          </StatusTag>
        );
        return r.entryRequirement ? (
          <Tooltip title={`Áp dụng khi: ${r.entryRequirement}`}>
            <span>{tag}</span>
          </Tooltip>
        ) : (
          tag
        );
      },
    },
    {
      key: 'tags',
      title: 'Tag',
      width: 190,
      render: (_, r) =>
        r.tags.length ? (
          <span className="flex flex-wrap gap-1">
            {r.tags.slice(0, 3).map((t) => (
              <span key={t} className="rounded-md bg-disabled px-1.5 py-0.5 text-xs text-ink-secondary">
                {t}
              </span>
            ))}
            {r.tags.length > 3 && (
              <Tooltip title={r.tags.slice(3).join(', ')}>
                <span className="rounded-md bg-disabled px-1.5 py-0.5 text-xs text-ink-muted">+{r.tags.length - 3}</span>
              </Tooltip>
            )}
          </span>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
    {
      key: 'content',
      title: 'Nội dung',
      width: 170,
      sorter: (a, b) => a.stats.lessons - b.stats.lessons,
      render: (_, r) => (
        <div className="leading-5">
          <p className="tabular text-ink">
            {r.stats.lessons} bài · {r.stats.chapters} chapter
          </p>
          <p className="tabular text-xs text-ink-muted">
            {r.stats.exams} mốc thi · {r.stats.materials} tài liệu
            {r.stats.emptyChapters > 0 && <span className="text-warning-700"> · {r.stats.emptyChapters} thiếu TL</span>}
          </p>
        </div>
      ),
    },
    {
      key: 'courses',
      title: 'Khóa áp dụng',
      width: 170,
      render: (_, r) =>
        r.courses.length ? (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              open(r.id, 'courses');
            }}
            className="flex flex-wrap gap-1 text-left"
          >
            {r.courses.slice(0, 2).map((c) => (
              <span
                key={c.id}
                className={cn(
                  'inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 font-mono text-xs',
                  c.isDefault ? 'bg-primary-50 text-primary-700' : 'bg-disabled text-ink-secondary',
                )}
                title={c.isDefault ? 'Lộ trình mặc định của khóa' : undefined}
              >
                {c.isDefault && <Star size={11} aria-hidden />}
                {c.code}
              </span>
            ))}
            {r.courses.length > 2 && <span className="rounded-md bg-disabled px-1.5 py-0.5 text-xs text-ink-muted">+{r.courses.length - 2}</span>}
          </button>
        ) : (
          <span className="text-[13px] text-warning-700">Chưa gắn khóa</span>
        ),
    },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 145,
      mobile: 'aside',
      render: (_, r) => {
        const s = CURRICULUM_STATUS[r.status];
        return (
          <StatusTag tone={s.tone} icon={s.icon} size="sm">
            {s.label}
          </StatusTag>
        );
      },
    },
    {
      key: 'updatedAt',
      title: 'Cập nhật',
      width: 140,
      sorter: (a, b) => dayjs(a.updatedAt).valueOf() - dayjs(b.updatedAt).valueOf(),
      render: (_, r) => (
        <div className="leading-5" title={fmtDateTime(r.updatedAt)}>
          <p className="tabular text-ink">{fmtDate(r.updatedAt)}</p>
          <p className="truncate text-xs text-ink-muted">{r.author}</p>
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Lộ trình"
        subtitle="Nội dung giảng dạy: bài học → chapter → tài liệu, mốc thi giữa kỳ / cuối kỳ"
        breadcrumb={[{ title: 'Đào tạo' }, { title: 'Lộ trình' }]}
        actions={
          editable && (
            <Button action="create" collapseOnMobile onClick={() => setDialog({ kind: 'create' })}>
              Tạo lộ trình
            </Button>
          )
        }
      />

      {/* Lọc nhanh theo trạng thái */}
      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo trạng thái">
        {[{ value: 'all', label: 'Tất cả' } as const, ...CURRICULUM_STATUS_OPTIONS].map((o) => {
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
        storageKey="academic-curriculum"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="lộ trình"
        searchKeys={['name', 'code', 'textbook', 'tagText']}
        onRowClick={(r) => open(r.id)}
        search={{
          placeholder: 'Tìm theo tên, mã, giáo trình, tag...',
          activeFilterCount: [level, tier, tag, courseId].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Cấp độ" value={level} onChange={setLevel} options={LEVEL_OPTIONS} showSearch={false} className="lg:!w-32" />
              <Select placeholder="Bậc" value={tier} onChange={setTier} options={TIER_OPTIONS} showSearch={false} className="lg:!w-32" />
              <Select placeholder="Tag" value={tag} onChange={setTag} options={allTags.map((t) => ({ value: t, label: t }))} className="lg:!w-36" />
              <Select placeholder="Khóa học" value={courseId} onChange={setCourseId} options={courses.map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` }))} className="lg:!w-52" />
            </>
          ),
          onReset: () => {
            setLevel(undefined);
            setTier(undefined);
            setTag(undefined);
            setCourseId(undefined);
            setStatus('all');
          },
        }}
        rowActions={(r) => (
          <>
            <Button action={editable && r.status === 'draft' ? 'edit' : 'view'} iconOnly size="sm" variant="soft" onClick={() => open(r.id)} />
            {editable && (
              <Button
                icon={Copy}
                iconOnly
                size="sm"
                tone="neutral"
                variant="soft"
                tooltip="Nhân bản — tạo bản nâng cao / biến thể"
                aria-label="Nhân bản"
                onClick={() => setDialog({ kind: 'clone', source: r })}
              />
            )}
          </>
        )}
        actionsWidth={110}
        empty={{
          title: 'Chưa có lộ trình',
          description: 'Tạo lộ trình, soạn bài học – chapter – tài liệu, rồi gắn vào khóa học.',
          action: editable ? <Button action="create" onClick={() => setDialog({ kind: 'create' })}>Tạo lộ trình</Button> : undefined,
        }}
      />

      {/* Tạo mới / nhân bản */}
      <ModalForm<CurriculumFormValues>
        key={dialog?.kind === 'clone' ? `clone-${dialog.source.id}` : 'create'}
        open={Boolean(dialog)}
        onClose={() => setDialog(null)}
        columns={2}
        title={dialog?.kind === 'clone' ? 'Nhân bản lộ trình' : 'Tạo lộ trình'}
        description={
          dialog?.kind === 'clone'
            ? `Sao chép toàn bộ bài học, chapter, tài liệu, mốc thi từ ${dialog.source.name} ${dialog.source.version} → bản nháp mới`
            : 'Tạo xong sẽ mở trang soạn nội dung'
        }
        submitAction="create"
        submitText={dialog?.kind === 'clone' ? 'Nhân bản' : 'Tạo lộ trình'}
        successMessage={dialog?.kind === 'clone' ? 'Đã nhân bản lộ trình' : 'Đã tạo lộ trình'}
        initialValues={
          dialog?.kind === 'clone'
            ? {
                ...dialog.source,
                // Gợi ý: nhân bản bản chuẩn → tạo bản nâng cao
                tier: dialog.source.tier === 'standard' ? 'advanced' : dialog.source.tier,
                name: dialog.source.tier === 'standard' ? `${dialog.source.name} (nâng cao)` : `${dialog.source.name} (bản sao)`,
                code: dialog.source.tier === 'standard' ? `${dialog.source.code}-ADV` : dialog.source.code,
                version: dialog.source.tier === 'standard' ? 'v1.0' : nextVersion(dialog.source.version),
              }
            : { tier: 'standard', version: 'v1.0', level: 'N5', tags: [], status: 'draft' }
        }
        onSubmit={async (v) => {
          await new Promise((r) => setTimeout(r, 500)); // giả lập gọi API
          const values = { ...v, name: v.name.trim(), tags: v.tags ?? [], entryRequirement: v.tier === 'advanced' ? v.entryRequirement?.trim() : undefined };
          const author = user?.fullName ?? 'Nhân viên';
          const created =
            dialog?.kind === 'clone' ? clone(dialog.source.id, values, author) : addCurriculum({ ...values, status: 'draft', author }, author);
          setStatus('all');
          setTimeout(() => {
            open(created.id, 'content');
            if (dialog?.kind === 'clone' && created.tier === 'advanced')
              notify.info({ title: 'Bản nâng cao đã được gắn vào các khóa của bản gốc', description: 'Phát hành xong, Giáo vụ chọn được khi mở lớp có đầu vào tốt.' });
          }, 300);
        }}
      >
        <CurriculumFormFields />
      </ModalForm>

      <CurriculumDetailDrawer curriculumId={openId} initialTab={openTab} onClose={() => setOpenId(null)} onOpen={(id) => open(id)} />
    </>
  );
}
