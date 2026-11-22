import { useMemo, useState } from 'react';
import { Drawer, Tabs } from 'antd';
import { Download, ExternalLink, FileAudio, FileImage, FileText, FileVideo, Link2, Search, type LucideIcon } from 'lucide-react';
import { Button, EmptyState, Select, TextInput, PageHeader, StatusTag, useNotify } from '@/components/common';
import { teacherName } from '@/features/academic/classes/constants';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { DOC_CATEGORIES, DOC_CATEGORY_OPTIONS, DOC_FILE_TYPES } from '@/features/library/constants';
import { DocViewer } from '@/features/library/DocViewer';
import { useLibraryStore } from '@/features/library/libraryStore';
import type { LibraryDoc } from '@/features/library/types';
import { usePortalStore } from '@/features/portal/portalStore';
import * as learningApi from '@/features/academic/classes/learningApi';
import { USE_API } from '@/lib/api/config';
import type { TeacherDoc } from '@/features/portal/types';
import { levelOf } from '@/features/portal/quiz';
import { activeOf, shortCode, useStudentId, useStudentLearning } from '@/features/portal/usePortal';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { fromNow } from '@/lib/date';
import { formatFileSize } from '@/lib/format';
import { includesText } from '@/lib/text';

const KIND_ICON: Record<TeacherDoc['kind'], LucideIcon> = { pdf: FileText, doc: FileText, image: FileImage, audio: FileAudio, video: FileVideo, link: Link2 };

/** Cổng HV › Tài liệu: tài liệu GV gửi cho lớp + Thư viện điện tử của trung tâm (theo trình độ) */
export default function StudentDocumentsPage() {
  const { user } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const studentId = useStudentId();
  const active = activeOf(useStudentLearning(studentId));
  const curricula = useCurriculumStore((s) => s.curricula);
  const teacherDocs = usePortalStore((s) => s.teacherDocs);
  const viewTeacherDoc = usePortalStore((s) => s.viewTeacherDoc);
  const library = useLibraryStore((s) => s.docs);
  const viewLib = useLibraryStore((s) => s.view);
  const [q, setQ] = useState('');
  const [category, setCategory] = useState<string>();
  const [open, setOpen] = useState<LibraryDoc | null>(null);

  const classIds = active.map((x) => x.cls.id);
  const branchIds = active.map((x) => x.cls.branchId);
  const levels = active.map((x) => levelOf(curricula.find((c) => c.id === x.cls.curriculumId)?.level));

  const fromTeachers = teacherDocs.filter((d) => d.classIds.some((id) => classIds.includes(id))).sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  const lib = useMemo(
    () =>
      library
        .filter((d) => d.status === 'published' && d.audience.includes('student') && (d.branchIds === 'all' || d.branchIds.some((b) => branchIds.includes(b))))
        .filter((d) => (!category || d.category === category) && (!q || includesText(`${d.title} ${d.subtitle ?? ''} ${d.tags.join(' ')}`, q)))
        // Tài liệu đúng trình độ đang học lên trước
        .sort((a, b) => Number(b.levels.some((l) => levels.includes(l))) - Number(a.levels.some((l) => levels.includes(l)))),
    [library, branchIds, levels, category, q],
  );

  const openTeacherDoc = (d: TeacherDoc) => {
    if (USE_API)
      return void learningApi
        .openInNewTab(() => learningApi.openTeacherDoc(d.id, false, studentId))
        .catch((e: Error) => notify.error({ title: e.message }));
    viewTeacherDoc(d.id, studentId);
    notify.toast.info(d.kind === 'link' ? `Mở ${d.url}` : `Đang tải ${d.fileName} (giả lập)`);
  };

  return (
    <>
      <PageHeader title="Tài liệu" subtitle="Tài liệu giáo viên gửi cho lớp bạn và thư viện điện tử của trung tâm." />
      <Tabs
        items={[
          {
            key: 'teacher',
            label: `Từ giáo viên (${fromTeachers.length})`,
            children: fromTeachers.length ? (
              <ul className="flex flex-col divide-y divide-line rounded-xl border border-line bg-surface">
                {fromTeachers.map((d) => {
                  const Icon = KIND_ICON[d.kind];
                  const unread = !d.viewedBy.includes(studentId);
                  return (
                    <li key={d.id} className="flex items-center gap-3 px-4 py-3">
                      <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-primary-50 text-primary-700">
                        <Icon size={18} aria-hidden />
                      </span>
                      <div className="min-w-0 flex-1">
                        <p className="flex items-center gap-2 font-medium text-ink">
                          <span className="truncate">{d.title}</span>
                          {unread && <span className="size-2 shrink-0 rounded-full bg-danger-600" aria-label="Mới" />}
                        </p>
                        <p className="truncate text-xs text-ink-muted">
                          {teacherName(d.teacherId)} · {active.filter((x) => d.classIds.includes(x.cls.id)).map((x) => shortCode(x.cls)).join(', ')} · {fromNow(d.createdAt)}
                          {d.size ? ` · ${formatFileSize(d.size)}` : ''}
                        </p>
                        {d.description && <p className="text-[13px] text-ink-secondary">{d.description}</p>}
                      </div>
                      <Button icon={d.kind === 'link' ? ExternalLink : Download} size="sm" variant="soft" onClick={() => openTeacherDoc(d)}>
                        {d.kind === 'link' ? 'Mở' : 'Tải'}
                      </Button>
                    </li>
                  );
                })}
              </ul>
            ) : (
              <EmptyState icon={FileText} title="Giáo viên chưa gửi tài liệu nào" />
            ),
          },
          {
            key: 'library',
            label: `Thư viện (${lib.length})`,
            children: (
              <>
                <div className="mb-3 flex flex-wrap gap-2">
                  <TextInput icon={Search} value={q} onChange={(e) => setQ(e.target.value)} placeholder="Tìm giáo trình, sách luyện thi..." className="!w-full sm:!w-72" />
                  <Select placeholder="Loại tài liệu" value={category} onChange={setCategory} options={DOC_CATEGORY_OPTIONS} className="!w-48" />
                </div>
                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  {lib.map((d) => {
                    const C = DOC_CATEGORIES[d.category];
                    const T = DOC_FILE_TYPES[d.fileType];
                    const mine = d.levels.some((l) => levels.includes(l));
                    return (
                      <button key={d.id} type="button" onClick={() => setOpen(d)} className="flex gap-3 rounded-xl border border-line bg-surface p-3 text-left hover:border-primary-300">
                        <span className="grid h-16 w-12 shrink-0 place-items-center rounded-md text-white" style={{ background: C.cover }}>
                          <C.icon size={18} aria-hidden />
                        </span>
                        <span className="min-w-0">
                          <span className="line-clamp-2 font-medium text-ink">{d.title}</span>
                          <span className="block text-xs text-ink-muted">
                            {T.label} · {d.levels.join(', ')}
                          </span>
                          {mine && (
                            <StatusTag tone="primary" icon={C.icon} size="sm">
                              Đúng trình độ
                            </StatusTag>
                          )}
                        </span>
                      </button>
                    );
                  })}
                </div>
                {!lib.length && <EmptyState icon={Search} title="Không tìm thấy tài liệu" />}
              </>
            ),
          },
        ]}
      />
      <Drawer open={Boolean(open)} onClose={() => setOpen(null)} width={isMobile ? '100%' : 760} destroyOnHidden title={open?.title}>
        {open && (
          <div className="flex flex-col gap-3">
            <DocViewer doc={open} onViewed={() => viewLib(open.id, user?.fullName ?? 'Học viên', 'Học viên')} />
            {open.description && <p className="text-[13px] text-ink-secondary">{open.description}</p>}
            {!open.allowDownload && <p className="text-xs text-ink-muted">Tài liệu chỉ xem trực tuyến (có watermark tên bạn), không tải về.</p>}
          </div>
        )}
      </Drawer>
    </>
  );
}
