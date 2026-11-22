import { useCallback, useEffect, useMemo, useState } from 'react';
import { Checkbox, Form, Modal } from 'antd';
import { CheckCircle2, Clock, Megaphone, PenLine, Printer, UserX, XCircle } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DataTable, EmptyState, ModalForm, NumberInput, PageHeader, StatusTag, TextArea, UserCell, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { OPTION_LABELS } from '@/features/academic/questions/constants';
import { Furigana } from '@/features/academic/questions/Furigana';
import { cn } from '@/lib/cn';
import { fmtDateTime } from '@/lib/date';
import { useClassStore } from '../classStore';
import * as learningApi from '../learningApi';
import type { ClassExam } from '../types';

interface Row extends learningApi.ExamResultRow {
  total: number;
  max: number;
  percent?: number;
  passed?: boolean;
}

/**
 * Kết quả kỳ thi (chế độ API — ND-06): danh sách lớp + trạng thái làm bài online, điểm từng phần (tự chấm / nhập tay), vắng thi,
 * công bố kết quả, bản in đề kèm đáp án. Dùng cho trang giáo vụ và cổng GV (<c>asTeacher</c>).
 */
export function ExamResultsBoard({
  classId,
  examId,
  asTeacher = false,
  backUrl,
  breadcrumb,
}: {
  classId: string;
  examId: string;
  asTeacher?: boolean;
  backUrl: string;
  breadcrumb?: { title: string; href?: string }[];
}) {
  const navigate = useNavigate();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const cls = useClassStore((s) => s.classes.find((c) => c.id === classId));
  const stored = useClassStore((s) => s.classExams.find((e) => e.classId === classId && e.examId === examId && e.status !== 'cancelled'));
  const [exam, setExam] = useState<ClassExam | undefined>(stored);
  const [rows, setRows] = useState<learningApi.ExamResultRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<Row | null>(null);
  const [paper, setPaper] = useState<learningApi.ExamPaperSection[] | null>(null);

  const reload = useCallback(async () => {
    if (!stored) return setLoading(false);
    try {
      const r = await learningApi.loadExamResults(stored.id, asTeacher);
      setExam(r.exam);
      setRows(r.rows);
    } catch (e) {
      notify.error({ title: (e as Error).message });
    } finally {
      setLoading(false);
    }
  }, [stored, asTeacher, notify]);

  useEffect(() => {
    void reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [stored?.id]);

  // Mở thẳng bằng đường dẫn (chưa vào trang lớp) → nạp kỳ thi của lớp trước
  useEffect(() => {
    if (stored) return;
    setLoading(true);
    void learningApi
      .loadClassLearning(classId, asTeacher)
      .catch(() => undefined)
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [classId, asTeacher]);

  const data: Row[] = useMemo(
    () =>
      rows
        .map((r) => {
          const total = r.result?.sections.reduce((n, s) => n + s.score, 0) ?? 0;
          const max = r.result?.sections.reduce((n, s) => n + s.max, 0) ?? exam?.maxScore ?? 0;
          const percent = r.result?.status === 'done' && max ? Math.round((total / max) * 1000) / 10 : undefined;
          return { ...r, total, max, percent, passed: percent !== undefined ? percent >= (exam?.passPercent ?? 60) : undefined };
        })
        .sort((a, b) => (b.percent ?? -1) - (a.percent ?? -1)),
    [rows, exam],
  );

  if (!cls || !exam)
    return (
      <EmptyState
        title={loading ? 'Đang tải…' : 'Chưa có kỳ thi cho mốc này'}
        description={loading ? undefined : 'Tạo và phát hành kỳ thi ở tab Bài thi của lớp.'}
        action={
          <Button action="back" onClick={() => navigate(backUrl)}>
            Quay lại
          </Button>
        }
      />
    );

  const done = data.filter((r) => r.result?.status === 'done');
  const avg = done.length ? Math.round(done.reduce((n, r) => n + (r.percent ?? 0), 0) / done.length) : undefined;
  const published = Boolean(exam.resultsPublishedAt);
  const ended = new Date(exam.endsAt).getTime() <= Date.now();

  const publishResults = async () => {
    const missing = data.filter((r) => !r.result).length;
    const ok = await confirm({
      title: published ? 'Công bố lại kết quả?' : 'Công bố kết quả?',
      description: `Học viên / phụ huynh sẽ thấy điểm, nhận xét${exam.mode === 'online' ? ' và đáp án' : ''}.${missing ? ` Còn ${missing} học viên chưa có kết quả.` : ''}`,
      confirmAction: 'send',
      confirmText: 'Công bố',
    });
    if (!ok) return;
    try {
      setExam(await learningApi.changeExam(exam.id, 'publish-results', asTeacher));
      notify.toast.success('Đã công bố kết quả');
    } catch (e) {
      notify.error({ title: (e as Error).message });
    }
  };

  const openPaper = async () => {
    try {
      setPaper((await learningApi.examPaper(exam.id, asTeacher)).sections);
    } catch (e) {
      notify.error({ title: (e as Error).message });
    }
  };

  const columns: DataColumn<Row>[] = [
    { key: 'fullName', title: 'Học viên', width: 210, mobile: 'title', render: (_, r) => <UserCell name={r.fullName} subtitle={r.code} size="sm" /> },
    {
      key: 'total',
      title: 'Điểm',
      width: 110,
      align: 'center',
      mobile: 'subtitle',
      render: (_, r) =>
        r.result?.status === 'done' ? (
          <span className="leading-5">
            <span className={cn('tabular block font-semibold', r.passed ? 'text-ink' : 'text-danger-600')}>
              {r.total}/{r.max}
            </span>
            <span className="tabular text-xs text-ink-muted">{r.percent}%</span>
          </span>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
    {
      key: 'state',
      title: 'Kết quả',
      width: 140,
      mobile: 'aside',
      render: (_, r) =>
        r.result?.status === 'absent' ? (
          <StatusTag tone="neutral" icon={UserX} size="sm">
            Vắng thi
          </StatusTag>
        ) : r.result ? (
          r.passed ? (
            <StatusTag tone="success" icon={CheckCircle2} size="sm">
              Đạt
            </StatusTag>
          ) : (
            <StatusTag tone="danger" icon={XCircle} size="sm">
              Chưa đạt
            </StatusTag>
          )
        ) : r.attempt === 'in_progress' ? (
          <StatusTag tone="info" icon={Clock} size="sm">
            Đang làm
          </StatusTag>
        ) : (
          <StatusTag tone="neutral" icon={Clock} size="sm">
            Chưa có điểm
          </StatusTag>
        ),
    },
    ...exam.sections.map(
      (sec, k): DataColumn<Row> => ({
        key: `sec-${k}`,
        title: sec.name,
        width: 110,
        align: 'center',
        render: (_, r) => {
          const s = r.result?.sections[k];
          return s ? <span className={cn('tabular text-[13px]', s.score / s.max < 0.5 && 'text-danger-600')}>{`${s.score}/${s.max}`}</span> : <span className="text-ink-muted">—</span>;
        },
      }),
    ),
    {
      key: 'duration',
      title: 'Làm bài',
      width: 140,
      render: (_, r) =>
        r.result?.startedAt ? (
          <span className="leading-5 text-[13px]">
            <span className="tabular block">{fmtDateTime(r.result.startedAt)}</span>
            <span className="text-xs text-ink-muted">{r.result.durationMinutes} phút</span>
          </span>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
    { key: 'comment', title: 'Nhận xét', width: 220, render: (_, r) => <span className="line-clamp-2 text-[13px] text-ink-secondary">{r.result?.comment ?? ''}</span> },
  ];

  return (
    <>
      <PageHeader
        breadcrumb={breadcrumb}
        title={exam.title}
        subtitle={`${cls.code} · ${exam.mode === 'online' ? 'Thi online' : 'Thi giấy'} · ${fmtDateTime(exam.startsAt)}, ${exam.durationMinutes} phút · ${exam.questionCount} câu / ${exam.maxScore} điểm · Đạt ≥ ${exam.passPercent}%${published ? ' · Đã công bố' : ''}`}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button icon={Printer} variant="outline" collapseOnMobile onClick={() => void openPaper()}>
              Đề & đáp án
            </Button>
            {exam.status === 'published' && (exam.mode === 'paper' || ended) && (
              <Button icon={Megaphone} collapseOnMobile onClick={() => void publishResults()}>
                {published ? 'Công bố lại' : 'Công bố kết quả'}
              </Button>
            )}
            <Button action="back" variant="outline" collapseOnMobile onClick={() => navigate(backUrl)}>
              Quay lại
            </Button>
          </div>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-2 lg:grid-cols-4">
        {[
          ['Có kết quả', `${data.filter((r) => r.result).length}/${data.length}`, `${data.filter((r) => r.result?.status === 'absent').length} vắng thi`],
          ['Điểm trung bình', avg !== undefined ? `${avg}%` : '—', `${done.length} bài`],
          ['Đạt', String(done.filter((r) => r.passed).length), `≥ ${exam.passPercent}%`],
          ['Đang làm (online)', String(data.filter((r) => r.attempt === 'in_progress').length), exam.mode === 'online' ? 'Hết giờ hệ thống tự nộp' : 'Thi giấy: nhập điểm tay'],
        ].map(([k, v, sub]) => (
          <div key={k} className="rounded-[var(--radius-card)] border border-line bg-surface px-3 py-2.5">
            <p className="text-xs text-ink-muted">{k}</p>
            <p className="tabular text-lg font-semibold text-ink">{v}</p>
            <p className="text-xs text-ink-muted">{sub}</p>
          </div>
        ))}
      </div>

      <DataTable<Row>
        storageKey="exam-results-api"
        columns={columns}
        data={data}
        rowKey="studentId"
        unitLabel="học viên"
        loading={loading}
        searchKeys={['fullName', 'code']}
        search={{ placeholder: 'Tìm học viên...' }}
        rowActions={(r) =>
          exam.status === 'published' ? (
            <Button icon={PenLine} iconOnly size="sm" tone="warning" variant="soft" tooltip="Nhập / sửa điểm" aria-label="Nhập điểm" onClick={() => setEditing(r)} />
          ) : null
        }
        actionsWidth={70}
      />

      <ModalForm<Record<string, number | string | boolean | undefined>>
        key={editing?.studentId ?? 'edit'}
        open={Boolean(editing)}
        onClose={() => setEditing(null)}
        columns={2}
        title={`${editing?.result ? 'Sửa' : 'Nhập'} điểm — ${editing?.fullName ?? ''}`}
        description={published ? 'Kết quả đã công bố — sửa điểm sẽ được ghi nhật ký.' : exam.mode === 'online' ? 'Điểm online do hệ thống tự chấm; sửa khi cần bù câu lỗi.' : undefined}
        submitAction="save"
        successMessage="Đã lưu kết quả"
        initialValues={
          editing
            ? {
                absent: editing.result?.status === 'absent',
                ...Object.fromEntries(exam.sections.map((_, k) => [`s${k}`, editing.result?.sections[k]?.score])),
                comment: editing.result?.comment,
              }
            : undefined
        }
        onSubmit={async (v) => {
          if (!editing) return;
          const absent = Boolean(v.absent);
          await learningApi.saveExamResult(
            exam.id,
            editing.studentId,
            { absent, scores: absent ? undefined : exam.sections.map((_, k) => Number(v[`s${k}`] ?? 0)), comment: (v.comment as string | undefined)?.trim() || undefined },
            asTeacher,
          );
          await reload();
        }}
      >
        <Form.Item name="absent" valuePropName="checked" className="md:col-span-2 !mb-2">
          <Checkbox>Vắng thi</Checkbox>
        </Form.Item>
        <Form.Item noStyle shouldUpdate>
          {({ getFieldValue }) =>
            !getFieldValue('absent') &&
            exam.sections.map((s, k) => (
              <Form.Item key={k} name={`s${k}`} label={`${s.name} (tối đa ${s.max})`} rules={[{ required: true, message: 'Nhập điểm' }]}>
                <NumberInput min={0} max={s.max} step={0.25} />
              </Form.Item>
            ))
          }
        </Form.Item>
        <Form.Item name="comment" label="Nhận xét" className="md:col-span-2">
          <TextArea maxLength={2000} />
        </Form.Item>
      </ModalForm>

      <Modal
        open={Boolean(paper)}
        onCancel={() => setPaper(null)}
        width={820}
        title={`${exam.title} — đề & đáp án`}
        footer={
          <Button icon={Printer} onClick={() => window.print()}>
            In
          </Button>
        }
      >
        <div className="flex max-h-[65vh] flex-col gap-4 overflow-y-auto">
          {paper?.map((s, k) => (
            <section key={k}>
              <p className="mb-1 font-semibold text-ink">
                Phần {k + 1}. {s.name} ({s.questions.length} câu × {s.points} điểm)
              </p>
              <ol className="flex flex-col gap-2 text-[13px]">
                {s.questions.map((q, j) => (
                  <li key={q.id}>
                    <p className="text-ink">
                      {j + 1}. <Furigana text={q.stem} />
                    </p>
                    <p className="text-ink-secondary">
                      {q.options.map((o, i) => (
                        <span key={i} className={cn('mr-4', i === q.answer && 'font-semibold text-success-700')}>
                          {OPTION_LABELS[i]}. <Furigana text={o} />
                        </span>
                      ))}
                    </p>
                  </li>
                ))}
              </ol>
            </section>
          ))}
        </div>
      </Modal>
      {confirmDialog}
    </>
  );
}
