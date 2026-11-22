import { useMemo, useState } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { BellRing, Bot, Paperclip, Trash2, CheckCircle2, Clock, Eye, FileAudio, FileImage, FileText, FileType2, Hourglass, Link2, PenLine, XCircle, type LucideIcon } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, DataTable, EmptyState, FileUpload, ModalForm, NumberInput, PageHeader, StatusTag, TextArea, TextInput, UserCell, useNotify, type DataColumn, type UploadItem } from '@/components/common';
import { AttachmentList, toAttachments } from '@/features/academic/classes/AttachmentList';
import { useClassStore } from '@/features/academic/classes/classStore';
import { ASSIGNMENT_TYPES, QUIZ_DIFFICULTY, QUIZ_SKILLS } from '@/features/academic/classes/constants';
import type { Submission, SubmissionFile } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { useCrmStore } from '@/features/crm/crmStore';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import * as learningApi from '@/features/academic/classes/learningApi';
import { USE_API } from '@/lib/api/config';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { formatFileSize } from '@/lib/format';

type SubState = 'missing' | 'waiting' | 'graded';

interface Row {
  id: string;
  studentId: string;
  fullName: string;
  code: string;
  sub?: Submission;
  late: boolean;
  state: SubState;
}

const FILE_ICON: Record<SubmissionFile['kind'], { icon: LucideIcon; color: string }> = {
  pdf: { icon: FileText, color: 'text-danger-600' },
  doc: { icon: FileType2, color: 'text-primary-600' },
  image: { icon: FileImage, color: 'text-success-600' },
  audio: { icon: FileAudio, color: 'text-warning-600' },
  link: { icon: Link2, color: 'text-ink-secondary' },
};

const STATE_TABS: { value: SubState | 'all'; label: string }[] = [
  { value: 'all', label: 'Tất cả' },
  { value: 'waiting', label: 'Chờ chấm' },
  { value: 'graded', label: 'Đã chấm' },
  { value: 'missing', label: 'Chưa nộp' },
];

/**
 * /admin/academic/classes/:id/assignments/:assignmentId — bài nộp của từng học viên
 * Cổng giáo viên dùng lại: /teacher/assignments/:id/:assignmentId (portal="teacher" → GV chấm được, quay về trang Bài tập)
 */
export default function AssignmentSubmissionsPage({ portal = 'staff' }: { portal?: 'staff' | 'teacher' }) {
  const { id, assignmentId } = useParams();
  const navigate = useNavigate();
  const notify = useNotify();
  const { can, user } = useAuth();
  const cls = useClassStore((s) => s.classes.find((c) => c.id === id));
  const assignment = useClassStore((s) => s.assignments.find((a) => a.id === assignmentId));
  const submissions = useClassStore((s) => s.submissions);
  const gradeSubmission = useClassStore((s) => s.gradeSubmission);
  const enrollments = useStudentStore((s) => s.enrollments);
  const students = useCrmStore((s) => s.students);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls?.curriculumId));
  const [tab, setTab] = useState<SubState | 'all'>('all');
  const [grading, setGrading] = useState<Row | null>(null);
  const [editingFiles, setEditingFiles] = useState(false);
  const updateAssignment = useClassStore((s) => s.updateAssignment);
  const editable = portal === 'teacher' || can('academic.classes');
  const asTeacher = portal === 'teacher';
  /** Chế độ API: tệp lưu trên server → xin link tạm rồi mở */
  const openFile = (index: number, submissionId?: string) =>
    void learningApi
      .openInNewTab(() => learningApi.fileLink(asTeacher ? 'teacher' : 'staff', assignment!.id, index, submissionId))
      .catch((e: Error) => notify.error({ title: e.message }));
  const classUrl = (cid: string) => (portal === 'teacher' ? '/teacher/assignments' : `/admin/academic/classes/${cid}?tab=assignments`);

  const rows: Row[] = useMemo(() => {
    if (!cls || !assignment) return [];
    const due = dayjs(assignment.dueDate).endOf('day');
    return enrollments
      .filter((e) => e.classId === cls.id && e.status !== 'dropped' && e.status !== 'transferred')
      .map((e) => {
        const st = students.find((s) => s.id === e.studentId);
        const sub = submissions.find((x) => x.assignmentId === assignment.id && x.studentId === e.studentId);
        return {
          id: e.studentId,
          studentId: e.studentId,
          fullName: st?.fullName ?? e.studentId,
          code: st?.code ?? '',
          sub,
          late: Boolean(sub && dayjs(sub.submittedAt).isAfter(due)),
          state: (!sub ? 'missing' : sub.score === undefined ? 'waiting' : 'graded') as SubState,
        };
      })
      .sort((a, b) => ['waiting', 'missing', 'graded'].indexOf(a.state) - ['waiting', 'missing', 'graded'].indexOf(b.state));
  }, [cls, assignment, enrollments, students, submissions]);

  if (!cls || !assignment)
    return (
      <EmptyState
        title="Không tìm thấy bài tập"
        action={<Button action="back" onClick={() => navigate(id ? classUrl(id) : '/admin/academic/classes')}>Quay lại</Button>}
      />
    );

  const closed = dayjs(assignment.dueDate).endOf('day').isBefore(dayjs());
  const counts = { all: rows.length, waiting: 0, graded: 0, missing: 0 } as Record<SubState | 'all', number>;
  rows.forEach((r) => counts[r.state]++);
  const graded = rows.filter((r) => r.sub?.score !== undefined);
  const avg = graded.length ? graded.reduce((n, r) => n + r.sub!.score!, 0) / graded.length : undefined;
  const lateCount = rows.filter((r) => r.late).length;
  const data = tab === 'all' ? rows : rows.filter((r) => r.state === tab);
  const lessonIdx = curriculum?.lessons.findIndex((l) => l.id === assignment.lessonId) ?? -1;
  const backToClass = () => navigate(classUrl(cls.id));

  const columns: DataColumn<Row>[] = [
    { key: 'fullName', title: 'Học viên', width: 210, mobile: 'title', sorter: (a, b) => a.fullName.localeCompare(b.fullName, 'vi'), render: (_, r) => <UserCell name={r.fullName} subtitle={r.code} size="sm" /> },
    {
      key: 'state',
      title: 'Trạng thái',
      width: 150,
      mobile: 'aside',
      render: (_, r) => (
        <span className="flex flex-col items-start gap-0.5">
          {r.state === 'missing' ? (
            <StatusTag tone={closed ? 'danger' : 'neutral'} icon={XCircle} size="sm">
              {closed ? 'Không nộp' : 'Chưa nộp'}
            </StatusTag>
          ) : r.state === 'waiting' ? (
            <StatusTag tone="warning" icon={Hourglass} size="sm">
              Chờ chấm
            </StatusTag>
          ) : r.sub?.quiz ? (
            <StatusTag tone="success" icon={Bot} size="sm">
              Tự chấm
            </StatusTag>
          ) : (
            <StatusTag tone="success" icon={CheckCircle2} size="sm">
              Đã chấm
            </StatusTag>
          )}
          {r.late && (
            <span className="inline-flex items-center gap-1 text-xs text-warning-700">
              <Clock size={11} aria-hidden /> Nộp muộn
            </span>
          )}
        </span>
      ),
    },
    {
      key: 'submittedAt',
      title: 'Nộp lúc',
      width: 140,
      mobile: 'subtitle',
      sorter: (a, b) => (a.sub?.submittedAt ?? '').localeCompare(b.sub?.submittedAt ?? ''),
      render: (_, r) => (r.sub ? <span className="tabular text-[13px]">{fmtDateTime(r.sub.submittedAt)}</span> : <span className="text-ink-muted">—</span>),
    },
    {
      key: 'files',
      title: assignment?.type === 'quiz' ? 'Kết quả làm bài' : 'Bài nộp',
      width: 280,
      render: (_, r) =>
        r.sub?.quiz ? (
          <div className="leading-5 text-[13px]">
            <p className="text-ink">
              Đúng <strong className="tabular">{r.sub.quiz.correct}/{r.sub.quiz.total}</strong> câu ({Math.round((r.sub.quiz.correct / r.sub.quiz.total) * 100)}%)
            </p>
            <p className="tabular text-xs text-ink-muted">
              {r.sub.quiz.attempts} lần làm · {r.sub.quiz.durationMinutes} phút
            </p>
          </div>
        ) : r.sub ? (
          <div className="flex flex-col gap-1">
            {r.sub.files.map((f, idx) => {
              const m = FILE_ICON[f.kind];
              const Icon = m.icon;
              const viaServer = USE_API && f.kind !== 'link';
              return (
                <a
                  key={f.name}
                  href={viaServer ? undefined : f.url}
                  target="_blank"
                  rel="noreferrer"
                  role={viaServer ? 'button' : undefined}
                  onClick={(e) => {
                    e.stopPropagation();
                    if (viaServer) {
                      e.preventDefault();
                      openFile(idx, r.sub!.id);
                    }
                  }}
                  className="inline-flex min-w-0 items-center gap-1.5 text-[13px] text-ink hover:text-primary-700 hover:underline"
                  title={f.name}
                >
                  <Icon size={14} className={cn('shrink-0', m.color)} aria-hidden />
                  <span className="truncate">{f.name}</span>
                  {f.size && <span className="shrink-0 text-xs text-ink-muted">{formatFileSize(f.size)}</span>}
                </a>
              );
            })}
            {r.sub.content && <p className="truncate text-xs text-ink-muted italic">“{r.sub.content}”</p>}
          </div>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
    {
      key: 'score',
      title: 'Điểm',
      width: 90,
      align: 'center',
      sorter: (a, b) => (a.sub?.score ?? -1) - (b.sub?.score ?? -1),
      render: (_, r) =>
        r.sub?.score !== undefined ? (
          <span className={cn('tabular font-semibold', r.sub.score < assignment.maxScore / 2 ? 'text-danger-600' : 'text-ink')}>
            {r.sub.score.toLocaleString('vi-VN')}/{assignment.maxScore}
          </span>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
    {
      key: 'comment',
      title: 'Nhận xét',
      width: 240,
      render: (_, r) =>
        r.sub?.comment ? (
          <div className="leading-5">
            <p className="line-clamp-2 text-[13px] text-ink-secondary">{r.sub.comment}</p>
            {r.sub.gradedBy && <p className="text-xs text-ink-muted">— {r.sub.gradedBy}</p>}
          </div>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        breadcrumb={
          portal === 'teacher'
            ? [{ title: 'Bài tập', href: '/teacher/assignments' }, { title: cls.code }, { title: 'Bài nộp' }]
            : [{ title: 'Đào tạo' }, { title: 'Lớp học', href: '/admin/academic/classes' }, { title: cls.code, href: `/admin/academic/classes/${cls.id}?tab=assignments` }, { title: 'Bài nộp' }]
        }
        title={assignment.title}
        subtitle={`${cls.code}${lessonIdx >= 0 ? ` · Bài ${lessonIdx + 1}: ${curriculum!.lessons[lessonIdx].title}` : ''} · Giao ${fmtDate(assignment.assignedAt)} · Hạn ${fmtDate(assignment.dueDate)}${closed ? ' (đã hết hạn)' : ''}`}
        actions={
          <>
            <Button action="back" variant="outline" collapseOnMobile onClick={backToClass}>
              {portal === 'teacher' ? 'Về Bài tập' : 'Về lớp'}
            </Button>
            {editable && counts.missing > 0 && (
              <Button icon={BellRing} variant="soft" collapseOnMobile onClick={() => notify.success({ title: `Đã nhắc ${counts.missing} học viên chưa nộp`, description: 'Gửi qua Cổng học viên + Zalo phụ huynh (giả lập)' })}>
                Nhắc nộp ({counts.missing})
              </Button>
            )}
          </>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-2 rounded-lg border border-line bg-surface px-3 py-2.5 text-[13px] text-ink-secondary">
        <StatusTag tone={ASSIGNMENT_TYPES[assignment.type].tone} icon={ASSIGNMENT_TYPES[assignment.type].icon} size="sm">
          {ASSIGNMENT_TYPES[assignment.type].label}
        </StatusTag>
        {assignment.quiz ? (
          <span>
            {assignment.quiz.questionCount} câu từ Ngân hàng câu hỏi · {assignment.quiz.skills.map((k) => QUIZ_SKILLS[k]).join(', ')} · độ khó {QUIZ_DIFFICULTY[assignment.quiz.difficulty].split(' (')[0].toLowerCase()}
            {assignment.quiz.timeLimitMinutes ? ` · ${assignment.quiz.timeLimitMinutes} phút` : ''} · {assignment.quiz.attempts} lần làm{assignment.quiz.shuffle ? ' · đảo đề' : ''}
          </span>
        ) : (
          assignment.description && <span>Yêu cầu: {assignment.description}</span>
        )}
      </div>

      {/* Bài nộp tay: đề bài GV gửi */}
      {assignment.type === 'upload' && (
        <div className="mb-4 flex flex-col gap-2 rounded-lg border border-line bg-surface px-3 py-2.5 sm:flex-row sm:items-center">
          <span className="inline-flex shrink-0 items-center gap-1.5 text-[13px] font-medium text-ink">
            <Paperclip size={15} aria-hidden /> Đề bài
          </span>
          <div className="min-w-0 flex-1">
            {assignment.attachments?.length ? <AttachmentList files={assignment.attachments} onOpen={USE_API ? (i) => openFile(i) : undefined} /> : <span className="text-[13px] text-ink-muted">Chưa đính kèm đề — học viên chỉ thấy phần yêu cầu.</span>}
          </div>
          {editable && (
            <Button size="sm" variant="soft" icon={Paperclip} onClick={() => setEditingFiles(true)}>
              {assignment.attachments?.length ? 'Sửa đề' : 'Gửi đề'}
            </Button>
          )}
        </div>
      )}

      <div className="mb-4 grid grid-cols-2 gap-2 lg:grid-cols-4">
        {[
          ['Đã nộp', `${rows.length - counts.missing}/${rows.length}`],
          ['Nộp muộn', String(lateCount)],
          assignment.quiz
            ? ['Tỷ lệ đúng TB', (() => { const q = rows.filter((r) => r.sub?.quiz); return q.length ? `${Math.round((q.reduce((n, r) => n + r.sub!.quiz!.correct / r.sub!.quiz!.total, 0) / q.length) * 100)}%` : '—'; })()]
            : ['Đã chấm', `${counts.graded}/${rows.length - counts.missing}`],
          ['Điểm trung bình', avg !== undefined ? `${avg.toLocaleString('vi-VN', { maximumFractionDigits: 1 })}/${assignment.maxScore}` : '—'],
        ].map(([k, v]) => (
          <div key={k} className="rounded-[var(--radius-card)] border border-line bg-surface px-3 py-2.5">
            <p className="text-xs text-ink-muted">{k}</p>
            <p className="tabular text-lg font-semibold text-ink">{v}</p>
          </div>
        ))}
      </div>

      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc bài nộp">
        {STATE_TABS.map((o) => {
          const active = tab === o.value;
          return (
            <button
              key={o.value}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setTab(o.value)}
              className={cn(
                'inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
                active ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
              )}
            >
              {o.label}
              <span className={cn('tabular rounded-full px-1.5 text-xs', active ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[o.value]}</span>
            </button>
          );
        })}
      </div>

      <DataTable<Row>
        storageKey="assignment-submissions"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="học viên"
        searchKeys={['fullName', 'code']}
        search={{ placeholder: 'Tìm học viên...' }}
        rowActions={
          editable
            ? (r) =>
                r.sub?.quiz ? (
                  <>
                    <Button
                      icon={Eye}
                      iconOnly
                      size="sm"
                      variant="soft"
                      tooltip="Xem bài làm từng câu"
                      aria-label="Xem bài làm"
                      onClick={() => notify.info({ title: 'Bài làm trắc nghiệm', description: 'Xem từng câu đúng / sai sẽ có khi làm module Ngân hàng câu hỏi.' })}
                    />
                    <Button icon={PenLine} iconOnly size="sm" tone="warning" variant="soft" tooltip="Nhận xét" aria-label="Nhận xét" onClick={() => setGrading(r)} />
                  </>
                ) : r.sub ? (
                  <Button icon={PenLine} size="sm" variant={r.state === 'waiting' ? 'solid' : 'soft'} onClick={() => setGrading(r)}>
                    {r.state === 'waiting' ? 'Chấm' : 'Sửa điểm'}
                  </Button>
                ) : (
                  <Button icon={BellRing} size="sm" tone="neutral" variant="ghost" onClick={() => notify.toast.success(`Đã nhắc ${r.fullName}`)}>
                    Nhắc
                  </Button>
                )
            : undefined
        }
        actionsWidth={120}
        empty={{ title: 'Không có học viên', description: 'Lớp chưa có học viên đang học.' }}
      />

      <ModalForm<{ keep: number[]; files?: UploadItem[]; link?: string }>
        key={`files-${editingFiles}`}
        open={editingFiles}
        onClose={() => setEditingFiles(false)}
        title="Đề bài kèm theo"
        description="Học viên thấy ngay trên Cổng học viên, mục Bài tập."
        submitAction="save"
        successMessage="Đã cập nhật đề bài"
        initialValues={{ keep: (assignment.attachments ?? []).map((_, i) => i) }}
        onSubmit={async (v) => {
          const kept = (assignment.attachments ?? []).filter((_, i) => v.keep.includes(i));
          if (USE_API) {
            const added = await learningApi.uploadAttachments(v.files, v.link ? [v.link] : [], asTeacher ? 'teacher' : 'staff');
            await learningApi.updateAssignment(assignment, { attachments: [...kept, ...added] }, asTeacher);
            return;
          }
          const next = [...kept, ...toAttachments(v.files, v.link ? [v.link] : [])];
          updateAssignment(assignment.id, { attachments: next.length ? next : undefined });
        }}
      >
        <Form.Item noStyle shouldUpdate>
          {({ getFieldValue, setFieldValue }) => {
            const keep: number[] = getFieldValue('keep') ?? [];
            const current = assignment.attachments ?? [];
            return current.length ? (
              <div className="mb-3">
                <p className="mb-1.5 text-[13px] font-medium text-ink">Tệp hiện có</p>
                <ul className="flex flex-col gap-1">
                  {current.map((f, i) =>
                    keep.includes(i) ? (
                      <li key={i} className="flex items-center gap-2 rounded-lg border border-line px-2.5 py-1.5 text-[13px]">
                        <span className="min-w-0 flex-1 truncate">{f.name}</span>
                        <Button size="sm" variant="ghost" tone="danger" icon={Trash2} iconOnly aria-label={`Bỏ ${f.name}`} onClick={() => setFieldValue('keep', keep.filter((k) => k !== i))} />
                      </li>
                    ) : null,
                  )}
                  {!keep.length && <li className="text-[13px] text-ink-muted">Đã bỏ hết tệp cũ.</li>}
                </ul>
              </div>
            ) : null;
          }}
        </Form.Item>
        <Form.Item name="keep" hidden noStyle />
        <Form.Item name="files" label="Thêm file đề" valuePropName="value">
          <FileUpload maxFiles={5} maxSizeMB={50} kind={['document', 'spreadsheet', 'image', 'audio']} />
        </Form.Item>
        <Form.Item name="link" label="Hoặc link đề (Google Drive, Docs...)" rules={[{ type: 'url', message: 'Link không hợp lệ' }]}>
          <TextInput placeholder="https://" />
        </Form.Item>
      </ModalForm>

      <ModalForm<{ score: number; comment?: string }>
        key={grading?.id ?? 'grade'}
        open={Boolean(grading)}
        onClose={() => setGrading(null)}
        title={`${grading?.sub?.quiz ? 'Nhận xét' : 'Chấm bài'} — ${grading?.fullName ?? ''}`}
        description={
          grading?.sub
            ? `Nộp ${fmtDateTime(grading.sub.submittedAt)}${grading.late ? ' · nộp muộn' : ''} · ${grading.sub.quiz ? `đúng ${grading.sub.quiz.correct}/${grading.sub.quiz.total} câu` : `${grading.sub.files.length} tệp`}`
            : undefined
        }
        submitAction="save"
        submitText="Lưu điểm"
        successMessage="Đã lưu điểm"
        initialValues={grading?.sub ? { score: grading.sub.score, comment: grading.sub.comment } : undefined}
        onSubmit={async (v) => {
          if (!grading?.sub) return;
          if (USE_API) await learningApi.gradeSubmission(grading.sub.id, v.score, v.comment?.trim() || undefined, asTeacher);
          else gradeSubmission(grading.sub.id, v.score, v.comment?.trim() || undefined, user?.fullName ?? 'Giáo viên');
        }}
      >
        <Form.Item
          name="score"
          label={`Điểm (thang ${assignment.maxScore})`}
          extra={grading?.sub?.quiz ? 'Bài trắc nghiệm — điểm do hệ thống tự chấm' : undefined}
          rules={[{ required: true, message: 'Nhập điểm' }]}
        >
          <NumberInput min={0} max={assignment.maxScore} step={0.25} disabled={Boolean(grading?.sub?.quiz)} />
        </Form.Item>
        <Form.Item name="comment" label="Nhận xét" extra="Học viên xem được trên Cổng học viên">
          <TextArea maxLength={500} placeholder="Điểm tốt, lỗi sai cần sửa..." />
        </Form.Item>
      </ModalForm>
    </>
  );
}
