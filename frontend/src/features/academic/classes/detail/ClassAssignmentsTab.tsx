import { useMemo, useState } from 'react';
import { Checkbox, Form, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { Trash2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DataTable, DateInput, ModalForm, NumberInput, Select, StatusTag, TextArea, TextInput, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { useCurriculumStore } from '../../curriculum/curriculumStore';
import { useClassStore } from '../classStore';
import * as learningApi from '../learningApi';
import { USE_API } from '@/lib/api/config';
import { ASSIGNMENT_TYPES, estimateBankQuestions, QUIZ_DIFFICULTY, QUIZ_DIFFICULTY_OPTIONS, QUIZ_SKILL_OPTIONS, QUIZ_SKILLS } from '../constants';
import type { Assignment, AssignmentType, ClassInfo, QuizDifficulty, QuizSkill } from '../types';
import { CheckCircle2, Clock, Hourglass } from 'lucide-react';

interface CreateValues {
  type: AssignmentType;
  title: string;
  dueDate: Dayjs;
  description?: string;
  lessonId?: string;
  lessonIds?: string[];
  skills: QuizSkill[];
  difficulty: QuizDifficulty;
  questionCount: number;
  timeLimitMinutes?: number;
  attempts: number;
  shuffle: boolean;
}

interface Row extends Assignment {
  lessonLabel: string;
  submitted: number;
  graded: number;
  late: number;
}

/** Tab Bài tập: bài tập giao cho lớp, tình hình nộp / chấm */
export function ClassAssignmentsTab({ cls }: { cls: ClassInfo }) {
  const { can } = useAuth();
  const { confirm, confirmDialog } = useConfirm();
  const navigate = useNavigate();
  const notify = useNotify();
  const all = useClassStore((s) => s.assignments);
  const submissions = useClassStore((s) => s.submissions);
  const addAssignment = useClassStore((s) => s.addAssignment);
  const removeAssignment = useClassStore((s) => s.removeAssignment);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const size = useStudentStore((s) => s.enrollments.filter((e) => e.classId === cls.id && e.status !== 'dropped' && e.status !== 'transferred').length);
  const [creating, setCreating] = useState(false);
  const editable = can('academic.classes') && cls.status !== 'cancelled';

  const lessonOptions = (curriculum?.lessons ?? []).map((l, i) => ({ value: l.id, label: `Bài ${i + 1}: ${l.title}` }));
  const lessonLabel = (id?: string) => {
    const i = curriculum?.lessons.findIndex((l) => l.id === id) ?? -1;
    return i >= 0 ? `Bài ${i + 1}: ${curriculum!.lessons[i].title}` : '—';
  };
  const rows: Row[] = useMemo(
    () =>
      all
        .filter((a) => a.classId === cls.id)
        .map((a) => {
          const subs = submissions.filter((x) => x.assignmentId === a.id);
          const due = dayjs(a.dueDate).endOf('day');
          return {
            ...a,
            lessonLabel: lessonLabel(a.lessonId),
            submitted: subs.length,
            graded: subs.filter((x) => x.score !== undefined).length,
            late: subs.filter((x) => dayjs(x.submittedAt).isAfter(due)).length,
          };
        })
        .sort((a, b) => b.assignedAt.localeCompare(a.assignedAt)),
    [all, submissions, cls.id, curriculum], // eslint-disable-line react-hooks/exhaustive-deps
  );

  const columns: DataColumn<Row>[] = [
    {
      key: 'title',
      title: 'Bài tập',
      width: 260,
      mobile: 'title',
      render: (_, r) => (
        <div className="min-w-0">
          <p className="truncate font-medium text-ink">{r.title}</p>
          <p className="truncate text-xs text-ink-muted">
            {r.quiz
              ? `${r.quiz.questionCount} câu · ${r.quiz.skills.map((k) => QUIZ_SKILLS[k]).join(', ')} · ${QUIZ_DIFFICULTY[r.quiz.difficulty].split(' ')[0]}${r.quiz.timeLimitMinutes ? ` · ${r.quiz.timeLimitMinutes} phút` : ''} · ${r.quiz.attempts} lần làm`
              : r.description}
          </p>
        </div>
      ),
    },
    {
      key: 'type',
      title: 'Loại',
      width: 130,
      render: (_, r) => {
        const t = ASSIGNMENT_TYPES[r.type];
        return (
          <StatusTag tone={t.tone} icon={t.icon} size="sm">
            {t.label}
          </StatusTag>
        );
      },
    },
    { key: 'lesson', title: 'Bài học', dataIndex: 'lessonLabel', width: 200 },
    { key: 'assignedAt', title: 'Giao lúc', width: 140, render: (_, r) => <span className="tabular text-[13px]">{fmtDateTime(r.assignedAt)}</span> },
    { key: 'dueDate', title: 'Hạn nộp', width: 110, mobile: 'subtitle', render: (_, r) => <span className="tabular">{fmtDate(r.dueDate)}</span> },
    {
      key: 'submitted',
      title: 'Đã nộp',
      width: 130,
      render: (_, r) => {
        const pct = size ? Math.min(100, Math.round((r.submitted / size) * 100)) : 0;
        return (
          <div className="flex flex-col gap-1">
            <span className="tabular text-xs text-ink-secondary">
              {r.submitted}/{size} HV · chấm {r.graded}
              {r.late > 0 && <span className="text-warning-700"> · {r.late} muộn</span>}
            </span>
            <span className="h-1.5 overflow-hidden rounded-full bg-disabled" aria-hidden>
              <span className="block h-full rounded-full bg-primary-600" style={{ width: `${pct}%` }} />
            </span>
          </div>
        );
      },
    },
    {
      key: 'status',
      title: 'Tình trạng',
      width: 140,
      mobile: 'aside',
      render: (_, r) => {
        const open = !dayjs(r.dueDate).isBefore(dayjs(), 'day');
        if (open)
          return (
            <StatusTag tone="info" icon={Clock} size="sm">
              Đang mở
            </StatusTag>
          );
        if (r.type === 'quiz')
          return (
            <StatusTag tone="success" icon={CheckCircle2} size="sm">
              Đã đóng · tự chấm
            </StatusTag>
          );
        return r.graded >= r.submitted ? (
          <StatusTag tone="success" icon={CheckCircle2} size="sm">
            Đã chấm xong
          </StatusTag>
        ) : (
          <StatusTag tone="warning" icon={Hourglass} size="sm">
            Chờ chấm {r.submitted - r.graded}
          </StatusTag>
        );
      },
    },
  ];

  return (
    <>
      <DataTable<Row>
        storageKey="class-assignments"
        columns={columns}
        data={rows}
        rowKey="id"
        unitLabel="bài tập"
        searchKeys={['title', 'lessonLabel']}
        onRowClick={(r) => navigate(`/admin/academic/classes/${cls.id}/assignments/${r.id}`)}
        search={{
          placeholder: 'Tìm bài tập...',
          actions: editable ? (
            <Button action="create" collapseOnMobile onClick={() => setCreating(true)}>
              Giao bài tập
            </Button>
          ) : undefined,
        }}
        rowActions={(r) => (
          <>
            <Button action="view" size="sm" variant="soft" onClick={() => navigate(`/admin/academic/classes/${cls.id}/assignments/${r.id}`)}>
              {r.type === 'quiz' ? 'Kết quả' : 'Bài nộp'}
            </Button>
            {editable && (
                <Button
                  icon={Trash2}
                  iconOnly
                  size="sm"
                  tone="danger"
                  variant="soft"
                  aria-label="Xóa bài tập"
                  onClick={async () => {
                    const ok = await confirm({ title: `Xóa "${r.title}"?`, description: r.submitted ? `${r.submitted} bài nộp sẽ bị xóa theo.` : undefined, tone: 'danger', confirmAction: 'delete' });
                    if (!ok) return;
                    if (USE_API) await learningApi.deleteAssignment(r.id).catch((e: Error) => notify.error({ title: e.message }));
                    else removeAssignment(r.id);
                  }}
                />
            )}
          </>
        )}
        actionsWidth={150}
        empty={{ title: 'Chưa giao bài tập', description: 'Giáo viên giao bài từ Cổng giáo viên, hoặc giao tại đây. Học viên nộp trên Cổng học viên.' }}
      />

      <ModalForm<CreateValues>
        open={creating}
        onClose={() => setCreating(false)}
        columns={2}
        width={760}
        title={`Giao bài tập cho lớp ${cls.code}`}
        description={`${size} học viên đang học sẽ nhận thông báo trên Cổng học viên`}
        submitAction="send"
        submitText="Giao bài"
        successMessage="Đã giao bài tập"
        initialValues={{ type: 'quiz', dueDate: dayjs().add(4, 'day'), skills: ['vocab', 'grammar'], difficulty: 'mixed', questionCount: 20, timeLimitMinutes: 25, attempts: 2, shuffle: true }}
        onSubmit={async (v) => {
          const input = {
            classId: cls.id,
            type: v.type,
            title: v.title.trim(),
            lessonId: v.lessonIds?.[0] ?? v.lessonId,
            dueDate: v.dueDate.format('YYYY-MM-DD'),
            description: v.description?.trim(),
            quiz:
              v.type === 'quiz'
                ? { lessonIds: v.lessonIds ?? [], skills: v.skills, difficulty: v.difficulty, questionCount: v.questionCount, timeLimitMinutes: v.timeLimitMinutes || undefined, attempts: v.attempts, shuffle: v.shuffle }
                : undefined,
          };
          if (!USE_API) return addAssignment(input);
          const warning = await learningApi.createAssignment(cls.id, input);
          if (warning) notify.warning({ title: 'Đã giao bài', description: warning });
        }}
      >
        <Form.Item name="type" label="Loại bài tập" rules={[{ required: true }]} className="md:col-span-2">
          <Radio.Group className="grid grid-cols-1 gap-2 sm:grid-cols-2">
            {(Object.keys(ASSIGNMENT_TYPES) as AssignmentType[]).map((k) => {
              const t = ASSIGNMENT_TYPES[k];
              const Icon = t.icon;
              return (
                <Radio key={k} value={k} className="!mr-0 rounded-lg border border-line p-3 [&.ant-radio-wrapper-checked]:border-primary-500 [&.ant-radio-wrapper-checked]:bg-primary-50">
                  <span className="flex flex-col gap-0.5">
                    <span className="inline-flex items-center gap-1.5 font-medium text-ink">
                      <Icon size={15} aria-hidden /> {k === 'quiz' ? 'Trắc nghiệm từ Ngân hàng câu hỏi' : 'Nộp bài (ảnh, file, ghi âm)'}
                    </span>
                    <span className="text-xs text-ink-muted">{t.hint}</span>
                  </span>
                </Radio>
              );
            })}
          </Radio.Group>
        </Form.Item>
        <Form.Item name="title" label="Tên bài tập" rules={[{ required: true, whitespace: true, message: 'Nhập tên' }]}>
          <TextInput placeholder="VD: Trắc nghiệm từ vựng bài 5" maxLength={120} />
        </Form.Item>
        <Form.Item name="dueDate" label="Hạn nộp" rules={[{ required: true, message: 'Chọn hạn' }]}>
          <DateInput disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
        </Form.Item>

        <Form.Item noStyle shouldUpdate={(a, b) => a.type !== b.type}>
          {({ getFieldValue }) =>
            getFieldValue('type') === 'quiz' ? (
              <>
                <Form.Item name="lessonIds" label="Lấy câu hỏi của bài" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn ít nhất 1 bài' }]} className="md:col-span-2">
                  <Select mode="multiple" placeholder="Chọn bài học (có thể ôn nhiều bài)" options={lessonOptions} />
                </Form.Item>
                <Form.Item name="skills" label="Kỹ năng" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn kỹ năng' }]} className="md:col-span-2">
                  <Checkbox.Group options={QUIZ_SKILL_OPTIONS} />
                </Form.Item>
                <Form.Item name="difficulty" label="Độ khó" rules={[{ required: true }]}>
                  <Select options={QUIZ_DIFFICULTY_OPTIONS} showSearch={false} allowClear={false} />
                </Form.Item>
                <Form.Item noStyle shouldUpdate={(a, b) => a.lessonIds !== b.lessonIds || a.skills !== b.skills}>
                  {({ getFieldValue: g }) => {
                    const available = estimateBankQuestions((g('lessonIds') as string[] | undefined)?.length ?? 0, (g('skills') as QuizSkill[] | undefined) ?? []);
                    return (
                      <Form.Item
                        name="questionCount"
                        label="Số câu"
                        extra={USE_API ? 'Ngân hàng thiếu câu → hệ thống bù câu cùng trình độ và báo lại' : available ? `Ngân hàng có ~${available} câu phù hợp` : 'Chọn bài & kỹ năng để xem số câu có sẵn'}
                        rules={[
                          { required: true, message: 'Nhập số câu' },
                          { validator: (_, v: number) => (!USE_API && available && v > available ? Promise.reject(new Error(`Ngân hàng chỉ có ~${available} câu phù hợp`)) : Promise.resolve()) },
                        ]}
                      >
                        <NumberInput min={5} max={100} unit="câu" />
                      </Form.Item>
                    );
                  }}
                </Form.Item>
                <Form.Item name="timeLimitMinutes" label="Giới hạn thời gian" extra="Bỏ trống = không giới hạn">
                  <NumberInput min={5} max={180} step={5} unit="phút" />
                </Form.Item>
                <Form.Item name="attempts" label="Số lần được làm" extra="Lấy điểm cao nhất" rules={[{ required: true }]}>
                  <NumberInput min={1} max={5} unit="lần" />
                </Form.Item>
                <Form.Item name="shuffle" valuePropName="checked" className="md:col-span-2">
                  <Checkbox>Mỗi học viên một đề khác nhau (đảo câu hỏi và đáp án)</Checkbox>
                </Form.Item>
                <p className="mb-4 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-[13px] text-primary-800 md:col-span-2">
                  Câu hỏi bốc ngẫu nhiên từ Ngân hàng câu hỏi theo tiêu chí trên khi học viên bắt đầu làm. Hệ thống tự chấm, giáo viên chỉ cần nhận xét.
                </p>
              </>
            ) : (
              <>
                <Form.Item name="lessonId" label="Thuộc bài học">
                  <Select placeholder="Chọn bài" options={lessonOptions} />
                </Form.Item>
                <div className="hidden md:block" />
              </>
            )
          }
        </Form.Item>
        <Form.Item name="description" label="Yêu cầu / hướng dẫn" className="md:col-span-2">
          <TextArea maxLength={1000} placeholder="Làm bài nào, nộp dạng gì (ảnh chụp vở, file, ghi âm)..." />
        </Form.Item>
      </ModalForm>
      {confirmDialog}
    </>
  );
}
