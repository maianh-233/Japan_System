import { useState } from 'react';
import { Tabs } from 'antd';
import { Download, ExternalLink, FileAudio, FileImage, FileText, FileVideo, Link2, type LucideIcon } from 'lucide-react';
import { Button, EmptyState, PageHeader, useNotify } from '@/components/common';
import { teacherName } from '@/features/academic/classes/constants';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { PortalLibrary } from '@/features/library/PortalLibrary';
import { usePortalStore } from '@/features/portal/portalStore';
import * as learningApi from '@/features/academic/classes/learningApi';
import { USE_API } from '@/lib/api/config';
import type { TeacherDoc } from '@/features/portal/types';
import { levelOf } from '@/features/portal/quiz';
import { activeOf, shortCode, useStudentId, useStudentLearning } from '@/features/portal/usePortal';
import { fromNow } from '@/lib/date';
import { formatFileSize } from '@/lib/format';

const KIND_ICON: Record<TeacherDoc['kind'], LucideIcon> = { pdf: FileText, doc: FileText, image: FileImage, audio: FileAudio, video: FileVideo, link: Link2 };

/** Cổng HV › Tài liệu: tài liệu GV gửi cho lớp + Thư viện điện tử của trung tâm (theo trình độ) */
export default function StudentDocumentsPage() {
  const notify = useNotify();
  const studentId = useStudentId();
  const active = activeOf(useStudentLearning(studentId));
  const curricula = useCurriculumStore((s) => s.curricula);
  const teacherDocs = usePortalStore((s) => s.teacherDocs);
  const viewTeacherDoc = usePortalStore((s) => s.viewTeacherDoc);
  const [libCount, setLibCount] = useState<number>();

  const classIds = active.map((x) => x.cls.id);
  const branchIds = active.map((x) => x.cls.branchId);
  const levels = active.map((x) => levelOf(curricula.find((c) => c.id === x.cls.curriculumId)?.level));

  const fromTeachers = teacherDocs.filter((d) => d.classIds.some((id) => classIds.includes(id))).sort((a, b) => b.createdAt.localeCompare(a.createdAt));

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
            label: `Thư viện${libCount === undefined ? '' : ` (${libCount})`}`,
            children: <PortalLibrary asTeacher={false} studentId={studentId} branchIds={branchIds} levels={levels} onCount={setLibCount} />,
          },
        ]}
      />
    </>
  );
}
