import { useEffect, useMemo, useState } from 'react';
import { Drawer, Spin } from 'antd';
import { Search } from 'lucide-react';
import { EmptyState, Select, StatusTag, TextInput, useNotify } from '@/components/common';
import type { QLevel } from '@/features/academic/questions/types';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { USE_API } from '@/lib/api/config';
import { includesText } from '@/lib/text';
import { DOC_CATEGORIES, DOC_CATEGORY_OPTIONS, DOC_FILE_TYPES } from './constants';
import { DocViewer } from './DocViewer';
import { endPortalView, loadPortalLibrary, startPortalView, type PortalDoc } from './libraryApi';
import { useLibraryStore } from './libraryStore';

interface Props {
  /** Cổng GV (TV-08) hay cổng HV / PH (TV-07) */
  asTeacher: boolean;
  /** HV đang xem (PH chọn con) */
  studentId?: string;
  /** Chế độ dữ liệu giả: chi nhánh + cấp độ lớp đang học / dạy (chế độ API server tự tính) */
  branchIds: string[];
  levels: (QLevel | undefined)[];
  /** Báo số tài liệu (nhãn tab) */
  onCount?: (n: number) => void;
}

/**
 * Thư viện trung tâm ở cổng (TV-07 / TV-08): tài liệu đang hiển thị, đúng đối tượng + chi nhánh, đúng cấp độ lớp đang học / dạy xếp trước.
 * Xem trực tuyến có watermark tên người xem; không có số liệu / thông tin nội bộ.
 */
export function PortalLibrary({ asTeacher, studentId, branchIds, levels, onCount }: Props) {
  const { user } = useAuth();
  const notify = useNotify();
  const { isMobile } = useBreakpoint();
  const mockDocs = useLibraryStore((s) => s.docs);
  const viewMock = useLibraryStore((s) => s.view);
  const [serverDocs, setServerDocs] = useState<PortalDoc[] | null>(null);
  const [q, setQ] = useState('');
  const [category, setCategory] = useState<string>();
  const [open, setOpen] = useState<PortalDoc | null>(null);

  useEffect(() => {
    if (!USE_API) return;
    let alive = true;
    setServerDocs(null);
    loadPortalLibrary(asTeacher, studentId)
      .then((list) => alive && setServerDocs(list))
      .catch((e: Error) => {
        if (!alive) return;
        setServerDocs([]);
        notify.error({ title: 'Không tải được thư viện', description: e.message });
      });
    return () => {
      alive = false;
    };
  }, [asTeacher, studentId, notify]);

  const all: PortalDoc[] = useMemo(() => {
    if (USE_API) return serverDocs ?? [];
    const audience = asTeacher ? 'teacher' : 'student';
    return mockDocs
      .filter((d) => d.status === 'published' && d.audience.includes(audience) && (d.branchIds === 'all' || d.branchIds.some((b) => branchIds.includes(b))))
      .map((d) => ({ ...d, matchesLevel: d.levels.some((l) => levels.includes(l)) }))
      .sort((a, b) => Number(b.matchesLevel) - Number(a.matchesLevel));
  }, [serverDocs, mockDocs, asTeacher, branchIds, levels]);

  const list = all.filter((d) => (!category || d.category === category) && (!q || includesText(`${d.title} ${d.subtitle ?? ''} ${d.tags.join(' ')} ${d.publisher ?? ''}`, q)));

  useEffect(() => onCount?.(all.length), [all.length, onCount]);

  return (
    <>
      <div className="mb-3 flex flex-wrap gap-2">
        <TextInput icon={Search} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm giáo trình, sách luyện thi..." className="!w-full sm:!w-72" />
        <Select placeholder="Loại tài liệu" value={category} onChange={setCategory} options={DOC_CATEGORY_OPTIONS} className="!w-48" />
      </div>
      {USE_API && serverDocs === null ? (
        <div className="grid place-items-center py-12">
          <Spin />
        </div>
      ) : (
        <>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {list.map((d) => {
              const C = DOC_CATEGORIES[d.category];
              const T = DOC_FILE_TYPES[d.fileType];
              return (
                <button key={d.id} type="button" onClick={() => setOpen(d)} className="flex gap-3 rounded-xl border border-line bg-surface p-3 text-left hover:border-primary-300">
                  <span className={`grid h-16 w-12 shrink-0 place-items-center rounded-md bg-gradient-to-br text-white ${C.cover}`}>
                    <C.icon size={18} aria-hidden />
                  </span>
                  <span className="min-w-0">
                    <span className="line-clamp-2 font-medium text-ink">{d.title}</span>
                    <span className="block text-xs text-ink-muted">
                      {T.label} · {d.levels.join(', ') || '—'}
                    </span>
                    {d.matchesLevel && (
                      <StatusTag tone="primary" icon={C.icon} size="sm">
                        {asTeacher ? 'Đúng cấp lớp đang dạy' : 'Đúng trình độ'}
                      </StatusTag>
                    )}
                  </span>
                </button>
              );
            })}
          </div>
          {!list.length && <EmptyState icon={Search} title={all.length ? 'Không tìm thấy tài liệu' : 'Thư viện chưa có tài liệu cho bạn'} />}
        </>
      )}
      <Drawer open={Boolean(open)} onClose={() => setOpen(null)} width={isMobile ? '100%' : 760} destroyOnHidden title={open?.title}>
        {open && (
          <div className="flex flex-col gap-3">
            <DocViewer
              doc={open}
              onViewed={() => viewMock(open.id, user?.fullName ?? (asTeacher ? 'Giáo viên' : 'Học viên'), asTeacher ? 'Giáo viên' : 'Học viên')}
              source={{ start: () => startPortalView(open.id, asTeacher, studentId), end: (id) => void endPortalView(id, asTeacher) }}
            />
            {open.description && <p className="text-[13px] text-ink-secondary">{open.description}</p>}
            {!open.allowDownload && <p className="text-xs text-ink-muted">Tài liệu chỉ xem trực tuyến (có watermark tên bạn), không tải về.</p>}
          </div>
        )}
      </Drawer>
    </>
  );
}
