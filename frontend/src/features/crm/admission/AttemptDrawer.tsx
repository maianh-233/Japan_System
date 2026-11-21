import { useEffect, useState } from 'react';
import { Drawer, Progress } from 'antd';
import { Check, Copy, NotebookPen, X } from 'lucide-react';
import { Button, StatusTag, useNotify } from '@/components/common';
import { Q_SKILLS } from '@/features/academic/questions/constants';
import { Furigana } from '@/features/academic/questions/Furigana';
import { useQuestionStore } from '@/features/academic/questions/questionStore';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDateTime } from '@/lib/date';
import { ATTEMPT_STATUS, courseLabel, SEND_CHANNELS } from '../constants';
import { useCrmStore } from '../crmStore';
import type { PlacementSection } from '../types';
import { USE_API } from '@/lib/api/config';
import { errorText, loadLead } from '../crmApi';
import { loadTestQuestions, logAttempt, type TestQuestion } from './admissionApi';
import { useAdmissionStore } from './admissionStore';
import { pctOf, testLink } from './placement';

interface Props {
  attemptId: string | null;
  onClose: () => void;
}

/** Kết quả 1 lượt làm test: điểm, theo kỹ năng, gợi ý trình độ / khóa, bài làm từng câu */
export function AttemptDrawer({ attemptId, onClose }: Props) {
  const { user } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const attempt = useAdmissionStore((s) => s.attempts.find((a) => a.id === attemptId));
  const test = useAdmissionStore((s) => s.tests.find((t) => t.id === attempt?.testId));
  const updateAttempt = useAdmissionStore((s) => s.updateAttempt);
  const lead = useCrmStore((s) => s.leads.find((l) => l.id === attempt?.leadId));
  const { addConsultation, updateLead } = useCrmStore();
  const storeQuestions = useQuestionStore((s) => s.questions);
  // Chế độ API: câu của đề lấy từ server (có đáp án — chỉ nhân viên), không phụ thuộc ngân hàng đã nạp
  const [testQs, setTestQs] = useState<TestQuestion[]>();
  useEffect(() => {
    setTestQs(undefined);
    if (USE_API && attempt?.status === 'done' && attempt.testId) loadTestQuestions(attempt.testId).then(setTestQs, () => undefined);
  }, [attempt?.testId, attempt?.status]);
  const questions: Pick<TestQuestion, 'id' | 'code' | 'level' | 'skill' | 'stem' | 'options' | 'answer'>[] = testQs ?? storeQuestions;

  const done = attempt?.status === 'done';
  const pct = attempt ? pctOf(attempt) : 0;
  const link = attempt ? testLink(attempt.token) : '';

  const logToLead = async () => {
    if (!attempt || !lead || !test) return;
    if (USE_API) {
      try {
        await logAttempt(attempt.id);
        await loadLead(lead.id).catch(() => undefined);
        notify.success({ title: 'Đã ghi vào lịch sử tư vấn', description: `Khóa quan tâm của ${lead.fullName} → ${courseLabel(attempt.suggestedCourse)}` });
      } catch (e) {
        notify.toast.error(errorText(e, 'Không ghi được kết quả'));
      }
      return;
    }
    addConsultation({
      leadId: lead.id,
      channel: 'message',
      result: 'test_booked',
      content: `Kết quả ${test.name}: ${attempt.correct}/${attempt.total} câu (${pct}%) — ${attempt.suggestedLevel}. Gợi ý khóa: ${courseLabel(attempt.suggestedCourse) ?? '—'}.`,
      staffName: user?.fullName ?? 'Hệ thống',
      system: true,
    });
    if (attempt.suggestedCourse) updateLead(lead.id, { interest: attempt.suggestedCourse });
    updateAttempt(attempt.id, { logged: true });
    notify.success({ title: 'Đã ghi vào lịch sử tư vấn', description: `Khóa quan tâm của ${lead.fullName} → ${courseLabel(attempt.suggestedCourse)}` });
  };

  return (
    <Drawer
      open={Boolean(attempt)}
      onClose={onClose}
      width={isMobile ? '100%' : 640}
      destroyOnHidden
      title={attempt && `${lead?.fullName ?? attempt.leadId} · ${test?.name ?? ''}`}
      extra={
        done && !attempt?.logged ? (
          <Button icon={NotebookPen} size="sm" onClick={logToLead}>
            Ghi vào lịch sử tư vấn
          </Button>
        ) : undefined
      }
    >
      {attempt && test && (
        <div className="flex flex-col gap-4 text-[13px]">
          <div className="flex flex-wrap items-center gap-2 text-ink-secondary">
            <StatusTag tone={ATTEMPT_STATUS[attempt.status].tone} icon={ATTEMPT_STATUS[attempt.status].icon} size="sm">
              {ATTEMPT_STATUS[attempt.status].label}
            </StatusTag>
            <span>
              Gửi qua {SEND_CHANNELS[attempt.channel]} bởi {attempt.sentBy} · {fmtDateTime(attempt.sentAt)}
            </span>
            {attempt.logged && <StatusTag tone="success" size="sm" icon={Check}>Đã ghi vào hồ sơ lead</StatusTag>}
          </div>

          {!done && (
            <div className="rounded-xl border border-line p-3">
              <p className="mb-1 text-xs text-ink-muted">Link làm bài (hết hạn {fmtDateTime(attempt.expiresAt)})</p>
              <div className="flex items-center gap-2">
                <code className="min-w-0 flex-1 truncate rounded bg-canvas px-2 py-1.5">{link}</code>
                <Button
                  icon={Copy}
                  size="sm"
                  variant="soft"
                  onClick={() => {
                    void navigator.clipboard?.writeText(link);
                    notify.toast.success('Đã sao chép link');
                  }}
                >
                  Sao chép
                </Button>
              </div>
            </div>
          )}

          {done && (
            <>
              <div className="grid gap-3 sm:grid-cols-[160px_1fr]">
                <div className="grid place-items-center rounded-xl border border-line p-3">
                  <Progress type="circle" size={110} percent={pct} format={() => <span className="tabular text-xl font-bold">{pct}%</span>} />
                  <p className="tabular mt-1 text-ink-secondary">
                    {attempt.correct}/{attempt.total} câu đúng
                  </p>
                </div>
                <div className="rounded-xl bg-primary-50 p-3 text-primary-800">
                  <p className="text-xs uppercase tracking-wide text-primary-700">Gợi ý xếp lớp</p>
                  <p className="mt-1 text-lg font-semibold">{attempt.suggestedLevel ?? '—'}</p>
                  <p>→ Khóa {courseLabel(attempt.suggestedCourse) ?? '—'}</p>
                  {/* Thang điểm */}
                  <div className="mt-3 flex h-2 overflow-hidden rounded-full bg-white">
                    {[...test.bands]
                      .sort((a, b) => a.minPct - b.minPct)
                      .map((b, i, arr) => (
                        <div
                          key={i}
                          className={cn('h-full border-r border-white', ['bg-primary-200', 'bg-primary-400', 'bg-primary-600', 'bg-primary-800'][i % 4])}
                          style={{ width: `${(arr[i + 1]?.minPct ?? 100) - b.minPct}%` }}
                          title={`${b.minPct}%+: ${b.level}`}
                        />
                      ))}
                  </div>
                  <div className="relative h-4">
                    <span className="absolute -translate-x-1/2 text-[11px] font-semibold" style={{ left: `${pct}%` }}>
                      ▲
                    </span>
                  </div>
                  <p className="text-[11px] text-primary-700">
                    {test.bands.map((b) => `${b.minPct}%+ ${b.level}`).join(' · ')}
                  </p>
                </div>
              </div>

              <div className="rounded-xl border border-line p-3">
                <p className="mb-2 font-semibold text-ink">Theo kỹ năng</p>
                <div className="flex flex-col gap-1.5">
                  {(Object.entries(attempt.bySkill ?? {}) as [PlacementSection['skill'], { correct: number; total: number }][]).map(([k, v]) => (
                    <div key={k} className="grid grid-cols-[90px_1fr_48px] items-center gap-2">
                      <span className="text-ink-secondary">{Q_SKILLS[k].label}</span>
                      <Progress percent={Math.round((v.correct / v.total) * 100)} showInfo={false} size="small" strokeColor={v.correct / v.total < 0.5 ? 'var(--color-danger-600)' : undefined} />
                      <span className="tabular text-right">
                        {v.correct}/{v.total}
                      </span>
                    </div>
                  ))}
                </div>
              </div>

              <div>
                <p className="mb-2 font-semibold text-ink">Bài làm</p>
                <ol className="flex flex-col gap-2">
                  {test.questionIds.map((id, i) => {
                    const q = questions.find((x) => x.id === id);
                    if (!q) return null;
                    const picked = attempt.answers?.[id] ?? -1;
                    const ok = picked === q.answer;
                    return (
                      <li key={id} className={cn('rounded-lg border p-2.5', ok ? 'border-success-100' : 'border-danger-100 bg-danger-50/40')}>
                        <div className="flex items-start gap-2">
                          <span className={cn('mt-0.5 grid size-5 shrink-0 place-items-center rounded-full text-white', ok ? 'bg-success-600' : 'bg-danger-600')}>
                            {ok ? <Check size={12} aria-label="Đúng" /> : <X size={12} aria-label="Sai" />}
                          </span>
                          <div className="min-w-0 flex-1">
                            <p className="text-xs text-ink-muted">
                              Câu {i + 1} · {q.level} · {Q_SKILLS[q.skill].label} · {q.code}
                            </p>
                            <p className="text-ink">
                              <Furigana text={q.stem} />
                            </p>
                            <p className="mt-1 text-xs">
                              {picked < 0 ? (
                                <span className="text-warning-700">Bỏ trống</span>
                              ) : (
                                <span className={ok ? 'text-success-700' : 'text-danger-700'}>
                                  Chọn: <Furigana text={q.options[picked]} />
                                </span>
                              )}
                              {!ok && (
                                <span className="ml-3 text-success-700">
                                  Đúng: <Furigana text={q.options[q.answer]} />
                                </span>
                              )}
                            </p>
                          </div>
                        </div>
                      </li>
                    );
                  })}
                </ol>
              </div>
            </>
          )}
        </div>
      )}
    </Drawer>
  );
}
