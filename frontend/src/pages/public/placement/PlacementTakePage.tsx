import { useEffect, useRef, useState } from 'react';
import { Radio } from 'antd';
import { CheckCircle2, CircleAlert, ClipboardCheck, Hourglass, Send, Timer } from 'lucide-react';
import { useParams } from 'react-router-dom';
import { Button, useConfirm } from '@/components/common';
import { Logo } from '@/components/layout/Logo';
import { Furigana } from '@/features/academic/questions/Furigana';
import { USE_API } from '@/lib/api/config';
import { api, ApiError } from '@/lib/api/http';
import { cn } from '@/lib/cn';
import { fmtDateTime } from '@/lib/date';

/** Dữ liệu trang làm bài (không có đáp án đúng — CRM-13.E5) */
interface PublicAttempt {
  testName: string;
  description?: string | null;
  durationMin: number;
  questionCount: number;
  status: 'sent' | 'in_progress' | 'done' | 'expired';
  expiresAt: string;
  remainingSeconds: number;
  questions: { id: string; level: string; skill: string; format: string; stem: string; passage?: string | null; options: string[] }[];
  answers: Record<string, number>;
  correct?: number | null;
  total?: number | null;
}

const base = (token: string) => `/crm/public/placement/${encodeURIComponent(token)}`;
const call = <T,>(path: string, method?: string, body?: unknown) => api<T>(path, { method, body, noRefresh: true });
const mmss = (sec: number) => `${String(Math.floor(sec / 60)).padStart(2, '0')}:${String(sec % 60).padStart(2, '0')}`;

/**
 * Trang làm TEST XẾP LỚP công khai (/t/:token) — lead mở link Sale gửi, không cần tài khoản (CRM-13).
 * Bắt đầu → đồng hồ đếm ngược theo server; đáp án lưu dần (tải lại trang vẫn còn); hết giờ tự nộp phần đã làm.
 */
export default function PlacementTakePage() {
  const { token = '' } = useParams();
  const { confirm, confirmDialog } = useConfirm();
  const [data, setData] = useState<PublicAttempt>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [left, setLeft] = useState(0);
  const saveTimer = useRef<number>();
  const answersRef = useRef(answers);
  answersRef.current = answers;

  const apply = (d: PublicAttempt) => {
    setData(d);
    setAnswers(d.answers ?? {});
    setLeft(d.remainingSeconds);
  };
  const fail = (e: unknown) => setError(e instanceof ApiError ? e.message : 'Không mở được bài test. Kiểm tra lại đường link.');

  useEffect(() => {
    if (!USE_API) return setError('Trang làm bài chỉ chạy khi kết nối máy chủ.');
    call<PublicAttempt>(base(token)).then(apply, fail);
  }, [token]);

  const submit = async (auto = false) => {
    window.clearTimeout(saveTimer.current);
    setBusy(true);
    try {
      apply(await call<PublicAttempt>(`${base(token)}/submit`, 'POST', { answers: answersRef.current }));
    } catch (e) {
      if (!auto) fail(e);
    } finally {
      setBusy(false);
    }
  };

  // Đồng hồ: hết giờ → tự nộp phần đã làm
  useEffect(() => {
    if (data?.status !== 'in_progress') return;
    const t = window.setInterval(() => setLeft((s) => Math.max(0, s - 1)), 1000);
    return () => window.clearInterval(t);
  }, [data?.status]);
  useEffect(() => {
    if (data?.status === 'in_progress' && left === 0) void submit(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [left, data?.status]);

  const choose = (qid: string, idx: number) => {
    const next = { ...answers, [qid]: idx };
    setAnswers(next);
    // Lưu dần (gộp các lần chọn liên tiếp)
    window.clearTimeout(saveTimer.current);
    saveTimer.current = window.setTimeout(() => void call(`${base(token)}/answers`, 'PUT', { answers: next }).catch(() => undefined), 800);
  };

  const start = async () => {
    setBusy(true);
    try {
      apply(await call<PublicAttempt>(`${base(token)}/start`, 'POST'));
    } catch (e) {
      fail(e);
    } finally {
      setBusy(false);
    }
  };

  const askSubmit = async () => {
    const blank = (data?.questions.length ?? 0) - Object.keys(answers).length;
    const ok = await confirm({
      title: 'Nộp bài?',
      description: blank > 0 ? `Bạn còn ${blank} câu chưa trả lời. Sau khi nộp không sửa được nữa.` : 'Sau khi nộp không sửa được nữa.',
      confirmAction: 'send',
      confirmText: 'Nộp bài',
    });
    if (ok) void submit();
  };

  const done = Object.keys(answers).length;

  return (
    <main className="min-h-dvh bg-page px-4 py-6 sm:py-10">
      <div className="mx-auto flex w-full max-w-[760px] flex-col gap-4">
        <Logo className="mb-2" />

        {error && (
          <section className="flex items-start gap-3 rounded-2xl border border-danger-100 bg-surface p-5 text-ink">
            <CircleAlert className="mt-0.5 shrink-0 text-danger-600" size={20} aria-hidden />
            <p>{error}</p>
          </section>
        )}

        {!error && !data && <p className="text-ink-muted">Đang tải bài test…</p>}

        {data && (
          <section className="rounded-2xl border border-line bg-surface p-5 sm:p-6">
            <h1 className="text-xl font-semibold text-ink">{data.testName}</h1>
            {data.description && <p className="mt-1 text-ink-secondary">{data.description}</p>}
            <p className="mt-3 flex flex-wrap gap-x-5 gap-y-1 text-[13px] text-ink-muted">
              <span className="inline-flex items-center gap-1">
                <ClipboardCheck size={14} aria-hidden /> {data.questionCount} câu
              </span>
              <span className="inline-flex items-center gap-1">
                <Timer size={14} aria-hidden /> {data.durationMin} phút
              </span>
              {data.status === 'sent' && <span>Link hiệu lực đến {fmtDateTime(data.expiresAt)}</span>}
            </p>

            {data.status === 'sent' && (
              <div className="mt-5 flex flex-col gap-3">
                <p className="text-ink-secondary">Bấm “Bắt đầu” khi đã sẵn sàng — đồng hồ chạy ngay và hết giờ bài sẽ tự nộp. Đáp án được lưu dần, lỡ tải lại trang vẫn giữ.</p>
                <Button icon={Timer} onClick={start} loading={busy} className="self-start">
                  Bắt đầu làm bài
                </Button>
              </div>
            )}

            {data.status === 'done' && (
              <div className="mt-5 flex items-start gap-3 rounded-xl bg-success-50 p-4 text-success-800">
                <CheckCircle2 className="mt-0.5 shrink-0" size={20} aria-hidden />
                <div>
                  <p className="font-semibold">Đã nộp bài — cảm ơn bạn!</p>
                  {data.total ? (
                    <p className="mt-1">
                      Kết quả: <b className="tabular">{data.correct ?? 0}/{data.total}</b> câu đúng. Tư vấn viên sẽ liên hệ để gợi ý lớp phù hợp.
                    </p>
                  ) : (
                    <p className="mt-1">Tư vấn viên sẽ liên hệ để gợi ý lớp phù hợp.</p>
                  )}
                </div>
              </div>
            )}

            {data.status === 'expired' && (
              <div className="mt-5 flex items-start gap-3 rounded-xl bg-warning-50 p-4 text-warning-800">
                <Hourglass className="mt-0.5 shrink-0" size={20} aria-hidden />
                <p>Link làm bài đã hết hạn. Vui lòng liên hệ tư vấn viên để nhận link mới.</p>
              </div>
            )}
          </section>
        )}

        {data?.status === 'in_progress' && (
          <>
            <div className="sticky top-0 z-10 -mx-4 flex items-center justify-between gap-3 border-b border-line bg-page/95 px-4 py-2 backdrop-blur sm:mx-0 sm:rounded-xl sm:border">
              <span className={cn('tabular inline-flex items-center gap-1.5 font-semibold', left <= 60 ? 'text-danger-600' : 'text-ink')}>
                <Timer size={16} aria-hidden /> {mmss(left)}
              </span>
              <span className="tabular text-[13px] text-ink-muted">
                Đã làm {done}/{data.questions.length}
              </span>
              <Button icon={Send} size="sm" onClick={askSubmit} loading={busy}>
                Nộp bài
              </Button>
            </div>

            <ol className="flex flex-col gap-3">
              {data.questions.map((q, i) => (
                <li key={q.id} className="rounded-2xl border border-line bg-surface p-4">
                  <p className="text-xs text-ink-muted">Câu {i + 1}</p>
                  {q.passage && (
                    <p className="mt-2 whitespace-pre-line rounded-lg bg-canvas p-3 text-ink-secondary">
                      <Furigana text={q.passage} />
                    </p>
                  )}
                  <p className="mt-1 text-base text-ink">
                    <Furigana text={q.stem} />
                  </p>
                  <Radio.Group value={answers[q.id]} onChange={(e) => choose(q.id, e.target.value as number)} className="mt-3 flex flex-col gap-2">
                    {q.options.map((o, k) => (
                      <Radio key={k} value={k} className="!items-start">
                        <Furigana text={o} />
                      </Radio>
                    ))}
                  </Radio.Group>
                </li>
              ))}
            </ol>

            <Button icon={Send} onClick={askSubmit} loading={busy} className="self-end">
              Nộp bài
            </Button>
          </>
        )}
      </div>
      {confirmDialog}
    </main>
  );
}
