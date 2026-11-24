import { useEffect, useMemo, useState } from 'react';
import { Spin } from 'antd';
import { Eye, EyeOff, LayoutGrid, LibraryBig, List, Lock, TrendingUp, Upload, Users } from 'lucide-react';
import { Button, DataTable, DrawerForm, EmptyState, PageHeader, Select, StatCard, StatusTag, useNotify, type DataColumn } from '@/components/common';
import { Q_LEVELS, Q_SKILL_OPTIONS } from '@/features/academic/questions/constants';
import type { QLevel } from '@/features/academic/questions/types';
import { DOC_AUDIENCE, DOC_AUDIENCE_OPTIONS, DOC_CATEGORIES, DOC_FILE_TYPE_OPTIONS, DOC_FILE_TYPES } from '@/features/library/constants';
import { DocDetailDrawer, type DocTab } from '@/features/library/DocDetailDrawer';
import { DocFormFields, type DocFormValues } from '@/features/library/DocFormFields';
import { DocCover } from '@/features/library/DocViewer';
import { createDoc, linkOf, loadLibrary, setDocStatus, updateDoc } from '@/features/library/libraryApi';
import { useLibraryStore, type DocInput } from '@/features/library/libraryStore';
import type { DocCategory, LibraryDoc } from '@/features/library/types';
import { useAuth } from '@/hooks/useAuth';
import { USE_API } from '@/lib/api/config';
import { cn } from '@/lib/cn';
import { fmtDate, fromNow } from '@/lib/date';
import { normalizeText } from '@/lib/text';
import { ALL_BRANCHES } from '@/types/auth';

type Dialog = { kind: 'create' } | { kind: 'edit'; id: string } | null;

const NEW_DOC: Partial<DocFormValues> = { fileType: 'pdf', category: 'reference', levels: [], skills: [], audience: ['student', 'teacher'], watermark: true, allowDownload: false };

/** Thư viện tài liệu (TV-01 → 06). Xem trực tuyến có watermark, chặn tải về, thống kê lượt đọc. */
export default function LibraryPage() {
  const { user, branchId, can } = useAuth();
  const docs = useLibraryStore((s) => s.docs);
  const add = useLibraryStore((s) => s.add);
  const update = useLibraryStore((s) => s.update);
  const editable = can('library.documents');
  const notify = useNotify();
  const [loading, setLoading] = useState(USE_API);

  // Chế độ API: tải danh sách khi mở trang (người sửa được thấy cả tài liệu đang ẩn)
  useEffect(() => {
    if (!USE_API) return;
    loadLibrary(editable)
      .catch((e: Error) => notify.error({ title: 'Không tải được thư viện', description: e.message }))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editable]);

  const toggleStatus = (d: LibraryDoc) => {
    const status = d.status === 'published' ? 'hidden' : 'published';
    if (!USE_API) return update(d.id, { status });
    void setDocStatus(d.id, status).catch((e: Error) => notify.error({ title: e.message }));
  };

  const [category, setCategory] = useState<DocCategory | 'all'>('all');
  const [level, setLevel] = useState<QLevel>();
  const [skill, setSkill] = useState<string>();
  const [fileType, setFileType] = useState<string>();
  const [audience, setAudience] = useState<string>();
  const [keyword, setKeyword] = useState('');
  const [showHidden, setShowHidden] = useState(false);
  const [mode, setMode] = useState<'grid' | 'list'>('grid');
  const [dialog, setDialog] = useState<Dialog>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  const [openTab, setOpenTab] = useState<DocTab>('view');

  const open = (id: string, tab: DocTab = 'view') => {
    setOpenTab(tab);
    setOpenId(id);
  };

  const base = useMemo(() => {
    const k = normalizeText(keyword);
    return docs.filter(
      (d) =>
        (branchId === ALL_BRANCHES || d.branchIds === 'all' || d.branchIds.includes(branchId)) &&
        (showHidden || d.status === 'published') &&
        (!level || d.levels.includes(level)) &&
        (!skill || d.skills.includes(skill as never)) &&
        (!fileType || d.fileType === fileType) &&
        (!audience || d.audience.includes(audience as never)) &&
        (!k || normalizeText(`${d.title} ${d.subtitle ?? ''} ${d.tags.join(' ')} ${d.publisher ?? ''}`).includes(k)),
    );
  }, [docs, branchId, showHidden, level, skill, fileType, audience, keyword]);
  const counts = useMemo(() => {
    const c: Record<string, number> = { all: base.length };
    base.forEach((d) => (c[d.category] = (c[d.category] ?? 0) + 1));
    return c;
  }, [base]);
  const data = category === 'all' ? base : base.filter((d) => d.category === category);

  const stats = useMemo(() => {
    const pub = docs.filter((d) => d.status === 'published');
    return {
      total: pub.length,
      views14: pub.reduce((s, d) => s + d.stats.daily.reduce((a, b) => a + b, 0), 0),
      readers: pub.reduce((s, d) => s + d.stats.readers, 0),
      unread: pub.filter((d) => !d.stats.views).length,
      protectedCount: pub.filter((d) => !d.allowDownload).length,
    };
  }, [docs]);

  const editing = dialog?.kind === 'edit' ? docs.find((d) => d.id === dialog.id) : undefined;
  const toInput = (v: DocFormValues): DocInput => ({
    title: v.title.trim(),
    subtitle: v.subtitle?.trim() || undefined,
    description: v.description?.trim() || undefined,
    category: v.category,
    fileType: v.fileType,
    fileName: v.files?.[0]?.name ?? editing?.fileName,
    size: v.files?.[0]?.size ?? editing?.size,
    pages: v.fileType === 'pdf' ? v.pages : undefined,
    duration: v.fileType === 'audio' || v.fileType === 'video' ? v.duration : undefined,
    levels: v.levels,
    skills: v.skills ?? [],
    tags: v.tags ?? [],
    publisher: v.publisher?.trim() || undefined,
    year: v.year,
    audience: v.audience,
    branchIds: v.branchIds?.length ? v.branchIds : 'all',
    watermark: v.watermark,
    allowDownload: v.allowDownload,
    status: editing?.status ?? 'published',
  });

  const columns: DataColumn<LibraryDoc>[] = [
    {
      key: 'title',
      title: 'Tài liệu',
      width: 340,
      mobile: 'title',
      sorter: (a, b) => a.title.localeCompare(b.title, 'vi'),
      render: (_, d) => {
        const FT = DOC_FILE_TYPES[d.fileType];
        return (
          <div className={cn('flex min-w-0 gap-2.5', d.status === 'hidden' && 'opacity-60')}>
            <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary-50 text-primary-600">
              <FT.icon size={18} aria-hidden />
            </span>
            <div className="min-w-0">
              <p className="truncate font-medium text-ink">{d.title}</p>
              <p className="truncate text-xs text-ink-muted">
                {DOC_CATEGORIES[d.category].label}
                {d.pages && ` · ${d.pages} trang`}
                {d.duration && ` · ${d.duration} phút`}
              </p>
            </div>
          </div>
        );
      },
    },
    { key: 'levels', title: 'Cấp độ', width: 100, mobile: 'subtitle', render: (_, d) => d.levels.join(', ') },
    {
      key: 'audience',
      title: 'Ai xem',
      width: 150,
      render: (_, d) => <span className="text-[13px] text-ink-secondary">{d.audience.map((a) => DOC_AUDIENCE[a].label).join(', ')}</span>,
    },
    {
      key: 'protect',
      title: 'Bảo vệ',
      width: 120,
      render: (_, d) =>
        d.allowDownload ? (
          <span className="text-[13px] text-warning-700">Cho tải về</span>
        ) : (
          <span className="inline-flex items-center gap-1 text-[13px] text-success-700">
            <Lock size={13} aria-hidden /> Chỉ xem
          </span>
        ),
    },
    { key: 'views', title: 'Lượt xem', width: 100, align: 'right', sorter: (a, b) => a.stats.views - b.stats.views, render: (_, d) => <span className="tabular">{d.stats.views.toLocaleString('vi-VN')}</span> },
    {
      key: 'last',
      title: 'Xem gần nhất',
      width: 130,
      mobile: 'aside',
      render: (_, d) => (d.stats.lastViewedAt ? <span className="text-[13px] text-ink-secondary">{fromNow(d.stats.lastViewedAt)}</span> : <span className="text-[13px] text-warning-700">Chưa ai xem</span>),
    },
  ];

  const filters = (
    <div className="mb-4 flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-3 lg:flex-row lg:items-center">
      <input
        value={keyword}
        onChange={(e) => setKeyword(e.target.value)}
        placeholder="Tìm theo tên, tên tiếng Nhật, thẻ, nguồn..."
        className="h-10 min-w-0 flex-1 rounded-[var(--radius-control)] border border-line bg-surface px-3 text-sm outline-none focus:border-primary-500 focus:ring-[3px] focus:ring-primary-500/20"
      />
      <div className="flex flex-wrap gap-2">
        <Select placeholder="Cấp độ" value={level} onChange={setLevel} options={Q_LEVELS.map((l) => ({ value: l, label: l }))} showSearch={false} className="!w-28" />
        <Select placeholder="Kỹ năng" value={skill} onChange={setSkill} options={Q_SKILL_OPTIONS} showSearch={false} className="!w-32" />
        <Select placeholder="Dạng" value={fileType} onChange={setFileType} options={DOC_FILE_TYPE_OPTIONS} showSearch={false} className="!w-28" />
        <Select placeholder="Ai xem" value={audience} onChange={setAudience} options={DOC_AUDIENCE_OPTIONS} showSearch={false} className="!w-32" />
        {editable && (
          <Button icon={showHidden ? Eye : EyeOff} tone="neutral" variant={showHidden ? 'soft' : 'ghost'} onClick={() => setShowHidden((v) => !v)}>
            {showHidden ? 'Đang hiện tài liệu ẩn' : 'Hiện tài liệu ẩn'}
          </Button>
        )}
        <div className="inline-flex rounded-lg border border-line p-0.5" role="tablist" aria-label="Kiểu hiển thị">
          {(
            [
              ['grid', LayoutGrid, 'Lưới'],
              ['list', List, 'Danh sách'],
            ] as const
          ).map(([k, Icon, l]) => (
            <button key={k} type="button" role="tab" aria-selected={mode === k} aria-label={l} onClick={() => setMode(k)} className={cn('grid h-8 w-9 place-items-center rounded-md', mode === k ? 'bg-primary-600 text-white' : 'text-ink-secondary hover:bg-page')}>
              <Icon size={16} aria-hidden />
            </button>
          ))}
        </div>
      </div>
    </div>
  );

  return (
    <>
      <PageHeader
        title="Thư viện tài liệu"
        subtitle="Sách tham khảo, đề JLPT các năm, tài liệu tự học · xem trực tuyến có watermark, chặn tải về"
        breadcrumb={[{ title: 'Thư viện' }, { title: 'Thư viện tài liệu' }]}
        actions={
          editable && (
            <Button icon={Upload} onClick={() => setDialog({ kind: 'create' })} collapseOnMobile>
              Tải tài liệu lên
            </Button>
          )
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Tài liệu đang mở" value={stats.total} icon={LibraryBig} hint={`${stats.protectedCount} tài liệu chỉ xem trực tuyến`} />
        <StatCard label="Lượt xem 14 ngày" value={stats.views14.toLocaleString('vi-VN')} icon={TrendingUp} tone="success" />
        <StatCard label="Người đọc" value={stats.readers.toLocaleString('vi-VN')} icon={Users} tone="info" hint="Tổng người đọc khác nhau" />
        <StatCard label="Chưa ai đọc" value={stats.unread} icon={EyeOff} tone={stats.unread ? 'warning' : 'success'} hint="Cân nhắc giới thiệu cho lớp" />
      </div>

      {/* Nhóm tài liệu */}
      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Nhóm tài liệu">
        {[{ value: 'all' as const, label: 'Tất cả', icon: LibraryBig }, ...(Object.keys(DOC_CATEGORIES) as DocCategory[]).map((k) => ({ value: k, label: DOC_CATEGORIES[k].label, icon: DOC_CATEGORIES[k].icon }))].map((o) => {
          const active = category === o.value;
          return (
            <button
              key={o.value}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setCategory(o.value)}
              className={cn(
                'inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
                active ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
              )}
            >
              <o.icon size={15} aria-hidden />
              {o.label}
              <span className={cn('tabular rounded-full px-1.5 text-xs', active ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[o.value] ?? 0}</span>
            </button>
          );
        })}
      </div>

      {filters}

      {mode === 'grid' ? (
        data.length ? (
          <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
            {data.map((d) => (
              <li key={d.id}>
                <button
                  type="button"
                  onClick={() => open(d.id)}
                  className={cn(
                    'flex h-full w-full flex-col gap-2 rounded-[var(--radius-card)] border border-line bg-surface p-2.5 text-left shadow-[var(--shadow-soft-sm)] transition-shadow hover:shadow-[var(--shadow-soft-md)]',
                    d.status === 'hidden' && 'opacity-60',
                  )}
                >
                  <DocCover doc={d} className="aspect-[3/4] w-full" />
                  <p className="line-clamp-2 text-[13px] leading-snug font-medium text-ink">{d.title}</p>
                  <div className="mt-auto flex flex-wrap items-center justify-between gap-1 text-xs text-ink-muted">
                    <span className="inline-flex items-center gap-1">
                      <Eye size={12} aria-hidden /> {d.stats.views.toLocaleString('vi-VN')}
                    </span>
                    {d.status === 'hidden' ? (
                      <StatusTag tone="neutral" icon={EyeOff} size="sm">
                        Ẩn
                      </StatusTag>
                    ) : d.allowDownload ? (
                      <span className="text-warning-700">Cho tải</span>
                    ) : (
                      <span className="inline-flex items-center gap-1 text-success-700">
                        <Lock size={11} aria-hidden /> Chỉ xem
                      </span>
                    )}
                  </div>
                </button>
              </li>
            ))}
          </ul>
        ) : loading ? (
          <div className="grid place-items-center py-16">
            <Spin />
          </div>
        ) : (
          <EmptyState icon={LibraryBig} title="Không có tài liệu phù hợp" description="Đổi bộ lọc hoặc tải tài liệu mới lên." />
        )
      ) : (
        <DataTable<LibraryDoc>
          storageKey="library-documents"
          columns={columns}
          data={data}
          loading={loading}
          rowKey="id"
          unitLabel="tài liệu"
          onRowClick={(d) => open(d.id)}
          rowActions={
            editable
              ? (d) => (
                  <>
                    <Button action="edit" iconOnly size="sm" onClick={() => setDialog({ kind: 'edit', id: d.id })} />
                    <Button
                      icon={d.status === 'published' ? EyeOff : Eye}
                      iconOnly
                      size="sm"
                      tone="neutral"
                      variant="soft"
                      aria-label={d.status === 'published' ? 'Ẩn tài liệu' : 'Hiện tài liệu'}
                      tooltip={d.status === 'published' ? 'Ẩn tài liệu' : 'Hiện tài liệu'}
                      onClick={() => toggleStatus(d)}
                    />
                  </>
                )
              : undefined
          }
          actionsWidth={110}
          empty={{ title: 'Không có tài liệu phù hợp' }}
        />
      )}

      <DrawerForm<DocFormValues>
        key={editing ? `edit-${editing.id}` : 'create'}
        open={Boolean(dialog)}
        onClose={() => setDialog(null)}
        width={720}
        columns={2}
        title={editing ? 'Sửa thông tin tài liệu' : 'Tải tài liệu lên'}
        description={editing ? `Tải lên ${fmtDate(editing.uploadedAt)} bởi ${editing.uploadedBy}` : 'Mặc định: có watermark, chặn tải về.'}
        submitAction={editing ? 'save' : 'create'}
        submitText={editing ? 'Lưu' : 'Tải lên'}
        successMessage={editing ? 'Đã lưu tài liệu' : 'Đã tải tài liệu lên thư viện'}
        initialValues={editing ? { ...editing, url: linkOf(editing.id), branchIds: editing.branchIds === 'all' ? [] : editing.branchIds } : NEW_DOC}
        onSubmit={async (v) => {
          if (USE_API) {
            const file = v.files?.[0]?.file;
            const link = v.fileType === 'link' ? v.url?.trim() : undefined;
            if (editing) await updateDoc(editing.id, toInput(v), file, link);
            else {
              const d = await createDoc(toInput(v), file, link);
              setTimeout(() => open(d.id, 'access'), 300);
            }
            return;
          }
          await new Promise((r) => setTimeout(r, 500));
          if (editing) update(editing.id, toInput(v));
          else {
            const d = add(toInput(v), user?.fullName ?? 'Nhân viên');
            setTimeout(() => open(d.id, 'access'), 300);
          }
        }}
      >
        <DocFormFields editing={Boolean(editing)} />
      </DrawerForm>

      <DocDetailDrawer docId={openId} initialTab={openTab} editable={editable} onClose={() => setOpenId(null)} onEdit={(id) => setDialog({ kind: 'edit', id })} />
    </>
  );
}
