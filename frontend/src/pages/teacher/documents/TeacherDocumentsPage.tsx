import { useState } from 'react';
import { Form } from 'antd';
import { Eye, FileAudio, FileImage, FileText, FileVideo, Link2, Trash2, type LucideIcon } from 'lucide-react';
import { Button, EmptyState, FileUpload, ModalForm, PageHeader, Select, TextArea, TextInput, useConfirm, useNotify, type UploadItem } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { usePortalStore } from '@/features/portal/portalStore';
import type { TeacherDoc } from '@/features/portal/types';
import { shortCode, useTeacherId, useTeachingClasses } from '@/features/portal/usePortal';
import { useStudentStore } from '@/features/student/studentStore';
import { fmtDate } from '@/lib/date';
import { formatFileSize } from '@/lib/format';

const KIND_ICON: Record<TeacherDoc['kind'], LucideIcon> = { pdf: FileText, doc: FileText, image: FileImage, audio: FileAudio, video: FileVideo, link: Link2 };

const kindOf = (name: string): TeacherDoc['kind'] => {
  const ext = name.split('.').pop()?.toLowerCase() ?? '';
  if (ext === 'pdf') return 'pdf';
  if (['png', 'jpg', 'jpeg', 'webp'].includes(ext)) return 'image';
  if (['mp3', 'm4a', 'wav'].includes(ext)) return 'audio';
  if (['mp4', 'mov'].includes(ext)) return 'video';
  return 'doc';
};

interface Values {
  title: string;
  description?: string;
  link?: string;
  files?: UploadItem[];
  classIds: string[];
  sessionId?: string;
}

/** Cổng GV › Tài liệu của tôi: tài liệu tự soạn, gửi cho lớp mình dạy (khác Thư viện trung tâm) */
export default function TeacherDocumentsPage() {
  const me = useTeacherId();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const classes = useTeachingClasses();
  const allDocs = usePortalStore((s) => s.teacherDocs);
  const { addTeacherDoc, updateTeacherDoc, removeTeacherDoc } = usePortalStore();
  const enrollments = useStudentStore((s) => s.enrollments);
  const sessions = useClassStore((s) => s.sessions);
  const [editing, setEditing] = useState<TeacherDoc | 'new' | null>(null);
  const docs = allDocs.filter((d) => d.teacherId === me);
  const roster = (ids: string[]) => new Set(enrollments.filter((e) => ids.includes(e.classId) && e.status === 'studying').map((e) => e.studentId)).size;

  return (
    <>
      <PageHeader
        title="Tài liệu của tôi"
        subtitle="Tài liệu bạn tự soạn, chỉ học viên lớp được chọn thấy (mục Tài liệu trên Cổng học viên). Tài liệu chung của trung tâm nằm ở Thư viện."
        actions={
          <Button action="create" collapseOnMobile onClick={() => setEditing('new')}>
            Thêm tài liệu
          </Button>
        }
      />
      {!docs.length && <EmptyState icon={FileText} title="Chưa có tài liệu" action={<Button action="create" onClick={() => setEditing('new')}>Thêm tài liệu</Button>} />}
      <div className="grid gap-3 md:grid-cols-2">
        {docs.map((d) => {
          const Icon = KIND_ICON[d.kind];
          const total = roster(d.classIds);
          return (
            <article key={d.id} className="flex flex-col rounded-xl border border-line bg-surface p-4">
              <div className="flex items-start gap-3">
                <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-primary-50 text-primary-700">
                  <Icon size={20} aria-hidden />
                </span>
                <div className="min-w-0 flex-1">
                  <h3 className="font-semibold text-ink">{d.title}</h3>
                  <p className="truncate text-xs text-ink-muted">
                    {d.fileName}
                    {d.size ? ` · ${formatFileSize(d.size)}` : ''} · {fmtDate(d.createdAt)}
                  </p>
                </div>
              </div>
              {d.description && <p className="mt-2 text-[13px] text-ink-secondary">{d.description}</p>}
              <div className="mt-2 flex flex-wrap items-center gap-1.5 text-xs">
                {d.classIds.map((id) => {
                  const c = classes.find((x) => x.id === id);
                  return (
                    <span key={id} className="rounded bg-primary-50 px-1.5 py-0.5 text-primary-700">
                      {c ? shortCode(c) : id}
                    </span>
                  );
                })}
                <span className="inline-flex items-center gap-1 text-ink-muted">
                  <Eye size={12} aria-hidden /> {d.viewedBy.length}/{total} HV đã xem
                </span>
              </div>
              <div className="mt-auto flex gap-1.5 pt-3">
                <Button action="edit" size="sm" onClick={() => setEditing(d)} />
                <Button
                  icon={Trash2}
                  size="sm"
                  variant="ghost"
                  tone="danger"
                  onClick={async () => {
                    if (await confirm({ title: `Xóa “${d.title}”?`, description: 'Học viên không còn thấy tài liệu này.', tone: 'danger', confirmAction: 'delete' })) {
                      removeTeacherDoc(d.id);
                      notify.toast.success('Đã xóa tài liệu');
                    }
                  }}
                >
                  Xóa
                </Button>
              </div>
            </article>
          );
        })}
      </div>

      <ModalForm<Values>
        key={editing === 'new' ? 'new' : (editing?.id ?? 'none')}
        open={Boolean(editing)}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? 'Thêm tài liệu' : 'Sửa tài liệu'}
        submitAction="save"
        successMessage="Đã lưu — học viên lớp được chọn sẽ thấy ngay"
        initialValues={editing && editing !== 'new' ? { ...editing, link: editing.kind === 'link' ? editing.url : undefined } : { classIds: classes.filter((c) => c.status === 'ongoing').slice(0, 1).map((c) => c.id) }}
        onSubmit={(v) => {
          const file = v.files?.[0];
          // Nội dung tài liệu: file mới tải lên, hoặc link mới
          const content = file
            ? { fileName: file.name, kind: kindOf(file.name), size: file.size, url: file.url ?? (file.file ? URL.createObjectURL(file.file) : undefined) }
            : v.link
              ? { fileName: v.link, kind: 'link' as const, size: undefined, url: v.link }
              : undefined;
          const base = { title: v.title, description: v.description, classIds: v.classIds, sessionId: v.sessionId };
          if (editing === 'new') {
            if (!content) throw new Error('Chọn file hoặc dán link');
            addTeacherDoc({ teacherId: me, ...base, ...content });
          } else if (editing) {
            const changed = content && (content.fileName !== editing.fileName || content.url !== editing.url);
            // Đổi file → học viên cần xem lại bản mới
            updateTeacherDoc(editing.id, { ...base, ...(changed ? { ...content, viewedBy: [] } : {}) });
          }
        }}
      >
        <Form.Item name="title" label="Tiêu đề" rules={[{ required: true, message: 'Nhập tiêu đề' }]}>
          <TextInput placeholder="Vd: Tổng hợp ngữ pháp bài 12" />
        </Form.Item>
        <Form.Item name="description" label="Mô tả">
          <TextArea rows={2} maxLength={300} />
        </Form.Item>
        {editing && editing !== 'new' && editing.kind !== 'link' && (
          <div className="mb-3 flex items-center gap-2 rounded-lg border border-line bg-page px-3 py-2 text-[13px]">
            {(() => {
              const Icon = KIND_ICON[editing.kind];
              return <Icon size={16} className="shrink-0 text-primary-600" aria-hidden />;
            })()}
            <span className="min-w-0 flex-1 truncate">
              File hiện tại: <b>{editing.fileName}</b>
              {editing.size ? ` · ${formatFileSize(editing.size)}` : ''}
            </span>
          </div>
        )}
        <Form.Item
          name="files"
          label={editing === 'new' ? 'File' : editing && editing.kind !== 'link' ? 'Thay bằng file mới (không bắt buộc)' : 'Đổi sang file (không bắt buộc)'}
          valuePropName="value"
          extra={editing !== 'new' ? 'Đổi file → học viên được tính là chưa xem bản mới.' : undefined}
        >
          <FileUpload maxFiles={1} maxSizeMB={50} kind={['document', 'spreadsheet', 'image', 'audio', 'video']} />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(a, b) => a.files !== b.files}>
          {({ getFieldValue }) => (
            <Form.Item name="link" label={editing === 'new' ? 'Hoặc link (YouTube, Google Drive...)' : 'Hoặc link mới (YouTube, Google Drive...)'} rules={[{ type: 'url', message: 'Link không hợp lệ' }]}>
              <TextInput icon={Link2} placeholder="https://" disabled={Boolean(getFieldValue('files')?.length)} />
            </Form.Item>
          )}
        </Form.Item>
        <Form.Item name="classIds" label="Gửi cho lớp" rules={[{ required: true, message: 'Chọn ít nhất 1 lớp' }]}>
          <Select mode="multiple" options={classes.map((c) => ({ value: c.id, label: `${shortCode(c)} · ${c.courseLabel}` }))} />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(a, b) => a.classIds !== b.classIds}>
          {({ getFieldValue }) => {
            const ids: string[] = getFieldValue('classIds') ?? [];
            const list = sessions.filter((s) => ids.length === 1 && s.classId === ids[0] && s.status === 'scheduled').slice(0, 15);
            return list.length ? (
              <Form.Item name="sessionId" label="Gắn với buổi học (tùy chọn)">
                <Select placeholder="Không gắn" options={list.map((s) => ({ value: s.id, label: `Buổi ${s.no} · ${s.date} ${s.startTime}` }))} />
              </Form.Item>
            ) : null;
          }}
        </Form.Item>
      </ModalForm>
      {confirmDialog}
    </>
  );
}
