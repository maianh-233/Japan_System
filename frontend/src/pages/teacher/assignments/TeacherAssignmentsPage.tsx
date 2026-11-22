import { useMemo, useState } from 'react';
import { Checkbox, Form } from 'antd';
import dayjs from 'dayjs';
import { Link2, NotebookPen, Paperclip } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DataTable, DateInput, FileUpload, ModalForm, NumberInput, PageHeader, Select, StatCard, StatusTag, TextArea, TextInput, useNotify, type DataColumn, type UploadItem } from '@/components/common';
import { toAttachments } from '@/features/academic/classes/AttachmentList';
import { useClassStore } from '@/features/academic/classes/classStore';
import * as learningApi from '@/features/academic/classes/learningApi';
import { USE_API } from '@/lib/api/config';
import { ASSIGNMENT_TYPES, QUIZ_DIFFICULTY_OPTIONS, QUIZ_SKILL_OPTIONS } from '@/features/academic/classes/constants';
import type { Assignment, AssignmentType, QuizDifficulty, QuizSkill } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { shortCode, useTeachingClasses } from '@/features/portal/usePortal';
import { useStudentStore } from '@/features/student/studentStore';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';

interface Row extends Assignment {
  classCode: string;
  roster: number;
  submitted: number;
  waiting: number;
  graded: number;
}

interface FormValues {
  classId: string;
  type: AssignmentType;
  title: string;
  description?: string;
  lessonId?: string;
  dueDate: dayjs.Dayjs;
  maxScore: number;
  skills?: QuizSkill[];
  difficulty?: QuizDifficulty;
  questionCount?: number;
  timeLimitMinutes?: number;
  attempts?: number;
  shuffle?: boolean;
  /** Bài nộp tay: đề bài GV tự gửi */
  attachFiles?: UploadItem[];
  attachLink?: string;
}

/** Cổng GV › Bài tập: giao bài cho lớp mình dạy, theo dõi nộp bài, vào chấm */
export default function TeacherAssignmentsPage() {
  const navigate = useNavigate();
  const notify = useNotify();
  const classes = useTeachingClasses();
  const assignments = useClassStore((s) => s.assignments);
  const submissions = useClassStore((s) => s.submissions);
  const addAssignment = useClassStore((s) => s.addAssignment);
  const enrollments = useStudentStore((s) => s.enrollments);
  const curricula = useCurriculumStore((s) => s.curricula);
  const [classId, setClassId] = useState<string>();
  const [state, setState] = useState<string>();
  const [creating, setCreating] = useState(false);

  const rows: Row[] = useMemo(
    () =>
      assignments
        .filter((a) => classes.some((c) => c.id === a.classId))
        .map((a) => {
          const cls = classes.find((c) => c.id === a.classId)!;
          const subs = submissions.filter((x) => x.assignmentId === a.id);
          return {
            ...a,
            classCode: shortCode(cls),
            roster: enrollments.filter((e) => e.classId === a.classId && (e.status === 'studying' || e.status === 'completed')).length,
            submitted: subs.length,
            waiting: subs.filter((x) => x.score === undefined && a.type === 'upload').length,
            graded: subs.filter((x) => x.score !== undefined).length,
          };
        })
        .sort((a, b) => b.dueDate.localeCompare(a.dueDate)),
    [assignments, submissions, enrollments, classes],
  );
  const data = rows.filter((r) => (!classId || r.classId === classId) && (!state || (state === 'waiting' ? r.waiting > 0 : state === 'open' ? !dayjs(r.dueDate).endOf('day').isBefore(dayjs()) : dayjs(r.dueDate).endOf('day').isBefore(dayjs()))));
  const open = (r: Row) => navigate(`/teacher/assignments/${r.classId}/${r.id}`);

  const columns: DataColumn<Row>[] = [
    {
      key: 'title',
      title: 'Bài tập',
      width: 280,
      mobile: 'title',
      render: (_, r) => (
        <span className="leading-5">
          <span className="font-medium">{r.title}</span>
          <span className="block text-xs text-ink-muted">
            {r.classCode} · {ASSIGNMENT_TYPES[r.type].label}
            {r.quiz && ` · ${r.quiz.questionCount} câu`}
            {r.type === 'upload' && (r.attachments?.length ? ` · ${r.attachments.length} file đề` : ' · chưa gửi đề')}
          </span>
        </span>
      ),
    },
    {
      key: 'due',
      title: 'Hạn nộp',
      width: 130,
      sorter: (a, b) => a.dueDate.localeCompare(b.dueDate),
      render: (_, r) => {
        const closed = dayjs(r.dueDate).endOf('day').isBefore(dayjs());
        return (
          <span className="leading-5">
            <span className="tabular">{fmtDate(r.dueDate)}</span>
            <span className={cn('block text-xs', closed ? 'text-ink-muted' : 'text-primary-700')}>{closed ? 'Đã hết hạn' : `còn ${dayjs(r.dueDate).endOf('day').diff(dayjs(), 'day')} ngày`}</span>
          </span>
        );
      },
    },
    {
      key: 'submitted',
      title: 'Đã nộp',
      width: 140,
      render: (_, r) => (
        <span className="leading-5">
          <span className="tabular">
            {r.submitted}/{r.roster}
          </span>
          <span className="mt-1 block h-1.5 w-24 overflow-hidden rounded-full bg-canvas">
            <span className="block h-full bg-primary-500" style={{ width: `${r.roster ? (r.submitted / r.roster) * 100 : 0}%` }} />
          </span>
        </span>
      ),
    },
    {
      key: 'waiting',
      title: 'Chấm bài',
      width: 150,
      mobile: 'aside',
      render: (_, r) =>
        r.type === 'quiz' ? (
          <span className="text-xs text-ink-muted">Tự chấm</span>
        ) : r.waiting ? (
          <StatusTag tone="warning" icon={NotebookPen} size="sm">
            {r.waiting} chờ chấm
          </StatusTag>
        ) : (
          <span className="text-xs text-success-700">Đã chấm {r.graded}</span>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Bài tập"
        subtitle="Bài nộp file → GV chấm tay. Bài trắc nghiệm → câu hỏi bốc từ Ngân hàng câu hỏi, hệ thống tự chấm."
        actions={
          <Button action="create" collapseOnMobile onClick={() => setCreating(true)}>
            Giao bài tập
          </Button>
        }
      />
      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Chờ chấm" value={rows.reduce((n, r) => n + r.waiting, 0)} icon={NotebookPen} tone="warning" onClick={() => setState('waiting')} />
        <StatCard label="Đang mở" value={rows.filter((r) => !dayjs(r.dueDate).endOf('day').isBefore(dayjs())).length} icon={NotebookPen} tone="info" onClick={() => setState('open')} />
        <StatCard label="Tỉ lệ nộp" value={`${Math.round((rows.reduce((n, r) => n + r.submitted, 0) / Math.max(1, rows.reduce((n, r) => n + r.roster, 0))) * 100)}%`} icon={NotebookPen} tone="success" />
        <StatCard label="Tổng bài đã giao" value={rows.length} icon={NotebookPen} tone="primary" />
      </div>

      <DataTable<Row>
        storageKey="teacher-assignments"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="bài tập"
        searchKeys={['title', 'classCode']}
        onRowClick={open}
        search={{
          placeholder: 'Tìm bài tập...',
          activeFilterCount: [classId, state].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Lớp" value={classId} onChange={setClassId} options={classes.map((c) => ({ value: c.id, label: shortCode(c) }))} className="lg:!w-40" />
              <Select
                placeholder="Trạng thái"
                value={state}
                onChange={setState}
                showSearch={false}
                className="lg:!w-40"
                options={[
                  { value: 'waiting', label: 'Có bài chờ chấm' },
                  { value: 'open', label: 'Đang mở' },
                  { value: 'closed', label: 'Đã hết hạn' },
                ]}
              />
            </>
          ),
          onReset: () => {
            setClassId(undefined);
            setState(undefined);
          },
        }}
        empty={{ icon: NotebookPen, title: 'Chưa giao bài tập nào', action: <Button action="create" onClick={() => setCreating(true)}>Giao bài tập</Button> }}
      />

      <ModalForm<FormValues>
        open={creating}
        onClose={() => setCreating(false)}
        title="Giao bài tập"
        columns={2}
        width={720}
        submitAction="send"
        submitText="Giao bài"
        successMessage="Đã giao bài — học viên nhận thông báo trên Cổng học viên"
        initialValues={{ type: 'upload', maxScore: 10, dueDate: dayjs().add(7, 'day'), difficulty: 'mixed', questionCount: 15, attempts: 2, shuffle: true, skills: ['vocab', 'grammar'], classId: classes.find((c) => c.status === 'ongoing')?.id }}
        onSubmit={async (v) => {
          const quiz =
            v.type === 'quiz'
              ? { lessonIds: v.lessonId ? [v.lessonId] : [], skills: v.skills ?? [], difficulty: v.difficulty ?? 'mixed', questionCount: v.questionCount ?? 10, timeLimitMinutes: v.timeLimitMinutes, attempts: v.attempts ?? 1, shuffle: Boolean(v.shuffle) }
              : undefined;
          if (USE_API) {
            const files = v.type === 'upload' ? await learningApi.uploadAttachments(v.attachFiles, v.attachLink ? [v.attachLink] : [], 'teacher') : [];
            const warning = await learningApi.createAssignment(
              v.classId,
              { type: v.type, title: v.title, description: v.description, lessonId: v.lessonId, dueDate: v.dueDate.format('YYYY-MM-DD'), maxScore: v.maxScore, quiz, attachments: files },
              true,
            );
            setClassId(v.classId);
            if (warning) notify.warning({ title: 'Đã giao bài', description: warning });
            return;
          }
          const attachments = v.type === 'upload' ? toAttachments(v.attachFiles, v.attachLink ? [v.attachLink] : []) : undefined;
          addAssignment({ classId: v.classId, type: v.type, title: v.title, description: v.description, lessonId: v.lessonId, dueDate: v.dueDate.format('YYYY-MM-DD'), maxScore: v.maxScore, quiz, attachments: attachments?.length ? attachments : undefined });
          setClassId(v.classId);
          notify.toast.success('Đã giao bài');
        }}
      >
        <Form.Item name="classId" label="Lớp" rules={[{ required: true, message: 'Chọn lớp' }]}>
          <Select options={classes.filter((c) => c.status === 'ongoing').map((c) => ({ value: c.id, label: `${shortCode(c)} · ${c.courseLabel}` }))} />
        </Form.Item>
        <Form.Item name="type" label="Loại">
          <Select options={(Object.keys(ASSIGNMENT_TYPES) as AssignmentType[]).map((k) => ({ value: k, label: `${ASSIGNMENT_TYPES[k].label} — ${ASSIGNMENT_TYPES[k].hint}` }))} allowClear={false} showSearch={false} />
        </Form.Item>
        <Form.Item name="title" label="Tiêu đề" rules={[{ required: true, message: 'Nhập tiêu đề' }]} className="md:col-span-2">
          <TextInput placeholder="Vd: Bài 12 — Luyện ngữ pháp ～ば" />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(a, b) => a.classId !== b.classId}>
          {({ getFieldValue }) => {
            const cls = classes.find((c) => c.id === getFieldValue('classId'));
            const cur = curricula.find((c) => c.id === cls?.curriculumId);
            return (
              <Form.Item name="lessonId" label="Bài học">
                <Select placeholder="Không gắn bài" options={cur?.lessons.map((l, i) => ({ value: l.id, label: `Bài ${i + 1}: ${l.title}` })) ?? []} />
              </Form.Item>
            );
          }}
        </Form.Item>
        <Form.Item name="dueDate" label="Hạn nộp" rules={[{ required: true, message: 'Chọn hạn' }]}>
          <DateInput disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item name="description" label="Hướng dẫn" className="md:col-span-2">
          <TextArea rows={2} maxLength={1000} placeholder="Yêu cầu, cách nộp (ảnh chụp vở, file ghi âm...)" />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(a, b) => a.type !== b.type}>
          {({ getFieldValue }) =>
            getFieldValue('type') === 'quiz' ? (
              <>
                <Form.Item name="skills" label="Kỹ năng" rules={[{ required: true, message: 'Chọn kỹ năng' }]}>
                  <Select mode="multiple" options={QUIZ_SKILL_OPTIONS} />
                </Form.Item>
                <Form.Item name="difficulty" label="Độ khó">
                  <Select options={QUIZ_DIFFICULTY_OPTIONS} allowClear={false} showSearch={false} />
                </Form.Item>
                <Form.Item name="questionCount" label="Số câu">
                  <NumberInput min={5} max={60} />
                </Form.Item>
                <Form.Item name="timeLimitMinutes" label="Thời gian (phút)" tooltip="Bỏ trống = không giới hạn">
                  <NumberInput min={5} max={120} />
                </Form.Item>
                <Form.Item name="attempts" label="Số lần làm (lấy điểm cao nhất)">
                  <NumberInput min={1} max={5} />
                </Form.Item>
                <Form.Item name="shuffle" valuePropName="checked" label=" ">
                  <Checkbox>Đảo câu & đáp án cho mỗi học viên</Checkbox>
                </Form.Item>
              </>
            ) : (
              <>
                <Form.Item name="maxScore" label="Thang điểm">
                  <NumberInput min={1} max={100} />
                </Form.Item>
                <div className="md:col-span-2">
                  <p className="mb-2 flex items-center gap-1.5 text-[13px] font-medium text-ink">
                    <Paperclip size={15} aria-hidden /> Đề bài của bạn
                    <span className="font-normal text-ink-muted">— file đề, phiếu bài tập, audio... học viên tải về để làm</span>
                  </p>
                  <Form.Item name="attachFiles" valuePropName="value" className="!mb-3">
                    <FileUpload maxFiles={5} maxSizeMB={50} kind={['document', 'spreadsheet', 'image', 'audio']} />
                  </Form.Item>
                  <Form.Item name="attachLink" label="Hoặc link đề (Google Drive, Docs...)" rules={[{ type: 'url', message: 'Link không hợp lệ' }]}>
                    <TextInput icon={Link2} placeholder="https://" />
                  </Form.Item>
                </div>
              </>
            )
          }
        </Form.Item>
      </ModalForm>
    </>
  );
}
