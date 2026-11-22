import { useMemo, useState } from 'react';
import { Form } from 'antd';
import { CheckCircle2, Eye, PenLine, UserX, XCircle } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { Button, DataTable, EmptyState, ModalForm, NumberInput, PageHeader, StatusTag, TextArea, UserCell, useNotify, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import type { ExamResult } from '@/features/academic/classes/types';
import { EXAM_KINDS } from '@/features/academic/curriculum/constants';
import { examScope, useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { useCrmStore } from '@/features/crm/crmStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { USE_API } from '@/lib/api/config';
import { ExamResultsBoard } from '@/features/academic/classes/exams/ExamResultsBoard';

/** % tối thiểu để đạt — sau lấy từ cấu hình kỳ thi ở module Kỳ thi */
const PASS_PERCENT = 60;

interface Row {
  id: string;
  fullName: string;
  code: string;
  result: ExamResult;
  total: number;
  max: number;
  percent: number;
  passed: boolean;
}

/** /admin/academic/classes/:id/exams/:examId — kết quả thi của từng học viên (chế độ API: kỳ thi thật trên server) */
export default function ExamResultsPage() {
  const { id, examId } = useParams();
  const code = useClassStore((s) => s.classes.find((c) => c.id === id)?.code);
  if (USE_API && id && examId)
    return (
      <ExamResultsBoard
        classId={id}
        examId={examId}
        backUrl={`/admin/academic/classes/${id}?tab=exams`}
        breadcrumb={[{ title: 'Đào tạo' }, { title: 'Lớp học', href: '/admin/academic/classes' }, { title: code ?? 'Lớp', href: `/admin/academic/classes/${id}?tab=exams` }, { title: 'Kết quả thi' }]}
      />
    );
  return <MockExamResultsPage />;
}

/** Chế độ mock: kết quả thi giả lập theo mốc lộ trình */
function MockExamResultsPage() {
  const { id, examId } = useParams();
  const navigate = useNavigate();
  const notify = useNotify();
  const { can } = useAuth();
  const cls = useClassStore((s) => s.classes.find((c) => c.id === id));
  const results = useClassStore((s) => s.examResults);
  const sessions = useClassStore((s) => s.sessions);
  const updateResult = useClassStore((s) => s.updateExamResult);
  const students = useCrmStore((s) => s.students);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls?.curriculumId));
  const [editing, setEditing] = useState<Row | null>(null);
  const [filter, setFilter] = useState<'all' | 'passed' | 'failed' | 'absent'>('all');

  const rows: Row[] = useMemo(
    () =>
      results
        .filter((r) => r.classId === id && r.examId === examId)
        .map((r) => {
          const st = students.find((s) => s.id === r.studentId);
          const total = r.sections.reduce((n, s) => n + s.score, 0);
          const max = r.sections.reduce((n, s) => n + s.max, 0) || 100;
          const percent = Math.round((total / max) * 100);
          return { id: r.id, fullName: st?.fullName ?? r.studentId, code: st?.code ?? '', result: r, total, max, percent, passed: r.status === 'done' && percent >= PASS_PERCENT };
        })
        .sort((a, b) => b.percent - a.percent),
    [results, students, id, examId],
  );

  const exam = curriculum?.exams.find((e) => e.id === examId);
  if (!cls || !exam)
    return (
      <EmptyState
        title="Không tìm thấy kỳ thi"
        action={<Button action="back" onClick={() => navigate(id ? `/admin/academic/classes/${id}?tab=exams` : '/admin/academic/classes')}>Quay lại</Button>}
      />
    );

  const meta = EXAM_KINDS[exam.kind];
  const session = sessions.find((s) => s.classId === cls.id && s.examId === exam.id);
  const scope = examScope(curriculum!, exam.id);
  const done = rows.filter((r) => r.result.status === 'done');
  const absent = rows.length - done.length;
  const avg = done.length ? Math.round(done.reduce((n, r) => n + r.percent, 0) / done.length) : undefined;
  const passRate = done.length ? Math.round((done.filter((r) => r.passed).length / done.length) * 100) : undefined;
  const sectionNames = done[0]?.result.sections.map((s) => s.name) ?? [];
  const data = rows.filter((r) => (filter === 'all' ? true : filter === 'absent' ? r.result.status === 'absent' : filter === 'passed' ? r.passed : r.result.status === 'done' && !r.passed));
  const back = () => navigate(`/admin/academic/classes/${cls.id}?tab=exams`);

  const columns: DataColumn<Row>[] = [
    { key: 'fullName', title: 'Học viên', width: 210, mobile: 'title', sorter: (a, b) => a.fullName.localeCompare(b.fullName, 'vi'), render: (_, r) => <UserCell name={r.fullName} subtitle={r.code} size="sm" /> },
    {
      key: 'total',
      title: 'Điểm',
      width: 120,
      align: 'center',
      mobile: 'subtitle',
      sorter: (a, b) => a.percent - b.percent,
      render: (_, r) =>
        r.result.status === 'absent' ? (
          <span className="text-ink-muted">—</span>
        ) : (
          <span className="leading-5">
            <span className={cn('tabular block font-semibold', r.passed ? 'text-ink' : 'text-danger-600')}>
              {r.total}/{r.max}
            </span>
            <span className="tabular text-xs text-ink-muted">{r.percent}%</span>
          </span>
        ),
    },
    {
      key: 'result',
      title: 'Kết quả',
      width: 130,
      mobile: 'aside',
      render: (_, r) =>
        r.result.status === 'absent' ? (
          <StatusTag tone="neutral" icon={UserX} size="sm">
            Vắng thi
          </StatusTag>
        ) : r.passed ? (
          <StatusTag tone="success" icon={CheckCircle2} size="sm">
            Đạt
          </StatusTag>
        ) : (
          <StatusTag tone="danger" icon={XCircle} size="sm">
            Chưa đạt
          </StatusTag>
        ),
    },
    ...sectionNames.map(
      (name, k): DataColumn<Row> => ({
        key: `sec-${k}`,
        title: name,
        width: 130,
        align: 'center',
        render: (_, r) => {
          const s = r.result.sections[k];
          return s ? (
            <span className={cn('tabular text-[13px]', s.score / s.max < 0.5 && 'text-danger-600')}>
              {s.score}/{s.max}
            </span>
          ) : (
            <span className="text-ink-muted">—</span>
          );
        },
      }),
    ),
    {
      key: 'duration',
      title: 'Làm bài',
      width: 140,
      render: (_, r) =>
        r.result.startedAt ? (
          <span className="leading-5 text-[13px]">
            <span className="tabular block">{fmtDateTime(r.result.startedAt)}</span>
            <span className="text-xs text-ink-muted">{r.result.durationMinutes} phút</span>
          </span>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
    { key: 'comment', title: 'Nhận xét', width: 220, render: (_, r) => <span className="line-clamp-2 text-[13px] text-ink-secondary">{r.result.comment ?? ''}</span> },
  ];

  return (
    <>
      <PageHeader
        breadcrumb={[{ title: 'Đào tạo' }, { title: 'Lớp học', href: '/admin/academic/classes' }, { title: cls.code, href: `/admin/academic/classes/${cls.id}?tab=exams` }, { title: 'Kết quả thi' }]}
        title={
          <span className="flex flex-wrap items-center gap-2">
            {exam.title}
            <StatusTag tone={meta.tone} icon={meta.icon} size="sm">
              {meta.label}
            </StatusTag>
          </span>
        }
        subtitle={`${cls.code}${scope ? ` · Phạm vi Bài ${scope.from}–${scope.to}` : ''}${session ? ` · Buổi ${session.no}, ${fmtDate(session.date)}` : ''} · Đạt ≥ ${PASS_PERCENT}%`}
        actions={
          <Button action="back" variant="outline" collapseOnMobile onClick={back}>
            Về lớp
          </Button>
        }
      />

      {!rows.length ? (
        <div className="rounded-[var(--radius-card)] border border-line bg-surface">
          <EmptyState
            title="Chưa có kết quả"
            description={session && session.date > new Date().toISOString().slice(0, 10) ? `Lớp thi vào ${fmtDate(session.date)}. Tạo kỳ thi (chọn câu hỏi từ Ngân hàng câu hỏi) trước ngày thi.` : 'Kỳ thi chưa được tổ chức hoặc chưa có bài làm.'}
          />
        </div>
      ) : (
        <>
          <div className="mb-4 grid grid-cols-2 gap-2 lg:grid-cols-4">
            {[
              ['Dự thi', `${done.length}/${rows.length}`, absent ? `${absent} vắng` : 'Đủ sĩ số'],
              ['Điểm trung bình', avg !== undefined ? `${avg}%` : '—', done.length ? `Cao nhất ${done[0].percent}% · thấp nhất ${done[done.length - 1].percent}%` : ''],
              ['Tỷ lệ đạt', passRate !== undefined ? `${passRate}%` : '—', `${done.filter((r) => r.passed).length} học viên đạt`],
              ['Chưa đạt', String(done.filter((r) => !r.passed).length), 'Cần ôn lại / thi lại'],
            ].map(([k, v, sub]) => (
              <div key={k} className="rounded-[var(--radius-card)] border border-line bg-surface px-3 py-2.5">
                <p className="text-xs text-ink-muted">{k}</p>
                <p className="tabular text-lg font-semibold text-ink">{v}</p>
                <p className="text-xs text-ink-muted">{sub}</p>
              </div>
            ))}
          </div>

          <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc kết quả">
            {(
              [
                ['all', 'Tất cả', rows.length],
                ['passed', 'Đạt', done.filter((r) => r.passed).length],
                ['failed', 'Chưa đạt', done.filter((r) => !r.passed).length],
                ['absent', 'Vắng thi', absent],
              ] as const
            ).map(([v, l, n]) => {
              const active = filter === v;
              return (
                <button
                  key={v}
                  type="button"
                  role="tab"
                  aria-selected={active}
                  onClick={() => setFilter(v)}
                  className={cn(
                    'inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
                    active ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
                  )}
                >
                  {l}
                  <span className={cn('tabular rounded-full px-1.5 text-xs', active ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{n}</span>
                </button>
              );
            })}
          </div>

          <DataTable<Row>
            storageKey="exam-results"
            columns={columns}
            data={data}
            rowKey="id"
            unitLabel="học viên"
            searchKeys={['fullName', 'code']}
            search={{ placeholder: 'Tìm học viên...' }}
            rowActions={(r) => (
              <>
                <Button
                  icon={Eye}
                  iconOnly
                  size="sm"
                  variant="soft"
                  tooltip="Xem bài làm"
                  aria-label="Xem bài làm"
                  disabled={r.result.status === 'absent'}
                  onClick={() => notify.info({ title: 'Bài làm chi tiết', description: 'Xem từng câu trả lời ở module Kỳ thi (sẽ làm cùng Ngân hàng câu hỏi).' })}
                />
                {can('academic.classes') && (
                  <Button icon={PenLine} iconOnly size="sm" tone="warning" variant="soft" tooltip="Sửa điểm / nhận xét" aria-label="Sửa điểm" onClick={() => setEditing(r)} />
                )}
              </>
            )}
            actionsWidth={110}
          />
        </>
      )}

      <ModalForm<Record<string, number | string | undefined>>
        key={editing?.id ?? 'edit'}
        open={Boolean(editing)}
        onClose={() => setEditing(null)}
        columns={2}
        title={`Sửa điểm — ${editing?.fullName ?? ''}`}
        description="Dùng khi chấm lại phần tự luận / phúc khảo. Phần trắc nghiệm do hệ thống tự chấm."
        submitAction="save"
        successMessage="Đã cập nhật điểm"
        initialValues={
          editing
            ? { ...Object.fromEntries(editing.result.sections.map((s, k) => [`s${k}`, s.score])), comment: editing.result.comment }
            : undefined
        }
        onSubmit={(v) => {
          if (!editing) return;
          const sections = (editing.result.sections.length ? editing.result.sections : (done[0]?.result.sections ?? [])).map((s, k) => ({ ...s, score: Number(v[`s${k}`] ?? 0) }));
          updateResult(editing.result.id, { sections, status: 'done', comment: (v.comment as string | undefined)?.trim() || undefined });
        }}
      >
        {(editing?.result.sections.length ? editing.result.sections : (done[0]?.result.sections ?? [])).map((s, k) => (
          <Form.Item key={s.name} name={`s${k}`} label={`${s.name} (tối đa ${s.max})`} rules={[{ required: true, message: 'Nhập điểm' }]}>
            <NumberInput min={0} max={s.max} step={0.5} />
          </Form.Item>
        ))}
        <Form.Item name="comment" label="Nhận xét" className="md:col-span-2">
          <TextArea maxLength={500} placeholder={editing?.result.status === 'absent' ? 'Thi bù ngày..., lý do vắng...' : 'Nhận xét của giáo viên'} />
        </Form.Item>
      </ModalForm>
    </>
  );
}
