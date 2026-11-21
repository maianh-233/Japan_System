import { useState, type ReactNode } from 'react';
import { Drawer, Switch } from 'antd';
import { Archive, CheckCircle2, Copy, RotateCcw, Send, Undo2, Headphones } from 'lucide-react';
import { Button, StatusTag, useConfirm, useNotify } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { useCurriculumStore } from '../curriculum/curriculumStore';
import { correctRate, Q_DIFFICULTY, Q_FORMATS, Q_SKILLS, Q_STATUS, rateTone } from './constants';
import { Furigana } from './Furigana';
import { QuestionCard } from './QuestionCard';
import { useQuestionStore } from './questionStore';
import { audioLink, changeStatus, duplicateQuestion, type QuestionAction } from './questionApi';
import { USE_API } from '@/lib/api/config';
import { ApiError } from '@/lib/api/http';
import type { QStatus } from './types';

interface Props {
  questionId: string | null;
  editable: boolean;
  /** Được duyệt / trả lại / ngừng dùng / mở lại (chế độ API: quyền approve) — mặc định = editable */
  approver?: boolean;
  onClose: () => void;
  onEdit: (id: string) => void;
  onOpen: (id: string) => void;
}

/** Chi tiết câu hỏi: xem trước (bật/tắt furigana, đáp án) · thống kê làm bài · duyệt / trả lại / ngừng dùng */
export function QuestionDetailDrawer({ questionId, editable, approver = editable, onClose, onEdit, onOpen }: Props) {
  const { isMobile } = useBreakpoint();
  const { user } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const q = useQuestionStore((s) => s.questions.find((x) => x.id === questionId));
  const setStatus = useQuestionStore((s) => s.setStatus);
  const duplicate = useQuestionStore((s) => s.duplicate);
  const curricula = useCurriculumStore((s) => s.curricula);
  const [furigana, setFurigana] = useState(true);
  const [showAnswer, setShowAnswer] = useState(true);

  if (!q) return <Drawer open={false} onClose={onClose} />;

  const me = user?.fullName ?? 'Nhân viên';
  const st = Q_STATUS[q.status];
  const sk = Q_SKILLS[q.skill];
  const rate = correctRate(q.stats);
  const cur = curricula.find((c) => c.id === q.curriculumId);
  const lessons = q.lessonIds.map((id) => {
    const i = cur?.lessons.findIndex((l) => l.id === id) ?? -1;
    return i >= 0 ? `Bài ${i + 1}: ${cur!.lessons[i].title}` : id;
  });

  /** Chế độ API: đổi trạng thái qua server, báo lý do nếu bị bỏ qua (vd tự duyệt câu của mình) */
  const act = async (action: QuestionAction, note?: string, success?: string) => {
    try {
      const r = await changeStatus(action, [q.id], note);
      if (r.skipped.length) notify.toast.warning(r.skipped[0].reason);
      else if (success) notify.toast.success(success);
    } catch (e) {
      notify.toast.error(e instanceof ApiError ? e.message : 'Không cập nhật được câu hỏi');
    }
  };
  const doStatus = (status: QStatus, action: QuestionAction, success?: string, note?: string) => {
    if (USE_API) return void act(action, note, success);
    setStatus([q.id], status, action === 'approve' || note ? { by: me, note } : undefined);
    if (success) notify.toast.success(success);
  };
  const playAudio = async () => {
    try {
      window.open(await audioLink(q.id), '_blank', 'noopener');
    } catch (e) {
      notify.toast.error(e instanceof ApiError ? e.message : 'Không mở được file nghe');
    }
  };

  const returnBack = async () => {
    const res = await confirm({
      title: 'Trả lại câu hỏi cho người soạn?',
      tone: 'warning',
      confirmAction: 'reject',
      confirmText: 'Trả lại',
      requireReason: { label: 'Cần sửa gì', placeholder: 'VD: đáp án 3 cũng đúng, cần đổi đáp án nhiễu' },
    });
    if (!res) return;
    doStatus('draft', 'return', 'Đã trả lại câu hỏi', res.reason);
  };
  const retire = async () => {
    const res = await confirm({
      title: 'Ngừng dùng câu hỏi này?',
      description: 'Không bốc vào bài tập / đề thi mới. Đề đã phát hành vẫn giữ nguyên.',
      tone: 'danger',
      confirmAction: 'reject',
      confirmText: 'Ngừng dùng',
      requireReason: { label: 'Lý do', placeholder: 'VD: lộ đề, nội dung lỗi thời' },
    });
    if (!res) return;
    doStatus('retired', 'retire', 'Đã ngừng dùng câu hỏi', res.reason);
  };

  const meta: [string, ReactNode][] = [
    ['Mã câu', <span className="font-mono">{q.code}</span>],
    ['Phân loại', `${q.level} · ${sk.label} (${sk.jp}) · ${Q_FORMATS[q.format].label}`],
    ['Độ khó dự kiến', Q_DIFFICULTY[q.difficulty].label],
    ['Lộ trình / bài', cur ? `${cur.name} — ${lessons.join(', ') || 'chưa gắn bài'}` : <span className="text-warning-700">Chưa gắn bài học</span>],
    ['Thẻ', q.tags.length ? q.tags.join(', ') : '—'],
    ['Người soạn', `${q.author} · ${fmtDate(q.createdAt)}`],
    ['Cập nhật', fmtDateTime(q.updatedAt)],
    ['Duyệt', q.review ? `${q.review.by} · ${fmtDate(q.review.at)}${q.review.note ? ` — ${q.review.note}` : ''}` : '—'],
  ];

  return (
    <Drawer
      open={Boolean(questionId)}
      onClose={onClose}
      width={isMobile ? '100%' : 760}
      destroyOnHidden
      title={
        <div className="flex min-w-0 flex-col gap-1 pr-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-mono text-base font-semibold">{q.code}</span>
            <StatusTag tone={st.tone} icon={st.icon} size="sm">
              {st.label}
            </StatusTag>
            <StatusTag tone={Q_DIFFICULTY[q.difficulty].tone} icon={sk.icon} size="sm">
              {q.level} · {sk.label}
            </StatusTag>
          </div>
          <span className="text-xs font-normal text-ink-muted">{st.hint}</span>
        </div>
      }
      extra={
        editable && (
          <div className="flex gap-1.5">
            <Button
              icon={Copy}
              size="sm"
              tone="neutral"
              variant="outline"
              collapseOnMobile
              onClick={async () => {
                try {
                  const c = USE_API ? await duplicateQuestion(q.id) : duplicate(q.id, me);
                  if (c) {
                    notify.toast.success(`Đã nhân bản → ${c.code} (nháp)`);
                    onOpen(c.id);
                  }
                } catch (e) {
                  notify.toast.error(e instanceof ApiError ? e.message : 'Không nhân bản được');
                }
              }}
            >
              Nhân bản
            </Button>
            {USE_API && q.audio && (
              <Button icon={Headphones} size="sm" tone="neutral" variant="outline" collapseOnMobile onClick={() => void playAudio()}>
                Nghe thử
              </Button>
            )}
            <Button action="edit" size="sm" collapseOnMobile onClick={() => onEdit(q.id)} />
          </div>
        )
      }
    >
      <div className="flex flex-col gap-5">
        {q.status === 'draft' && q.review?.note && (
          <p className="rounded-lg border border-warning-100 bg-warning-50 px-3 py-2 text-[13px] text-warning-700">
            Bị trả lại bởi {q.review.by}: {q.review.note}
          </p>
        )}

        {/* Xem trước */}
        <section>
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
            <h3 className="text-sm font-semibold text-ink">Xem trước</h3>
            <div className="flex gap-4 text-[13px] text-ink-secondary">
              <label className="inline-flex items-center gap-1.5">
                <Switch size="small" checked={furigana} onChange={setFurigana} /> Furigana
              </label>
              <label className="inline-flex items-center gap-1.5">
                <Switch size="small" checked={showAnswer} onChange={setShowAnswer} /> Đáp án & tỉ lệ chọn
              </label>
            </div>
          </div>
          <div className="rounded-[var(--radius-card)] border border-line p-4">
            <QuestionCard q={q} showAnswer={showAnswer} hideFurigana={!furigana} picks={showAnswer ? q.stats.picks : undefined} />
          </div>
          {showAnswer && q.explanation && (
            <p className="mt-2 rounded-lg bg-success-50 px-3 py-2 text-[13px] text-success-700">
              <strong>Giải thích: </strong>
              <Furigana text={q.explanation} hide={!furigana} />
            </p>
          )}
          {q.transcript && (
            <details className="mt-2 rounded-lg border border-line px-3 py-2 text-[13px]">
              <summary className="cursor-pointer font-medium text-ink-secondary">Lời thoại file nghe (chỉ GV)</summary>
              <p className="mt-2 whitespace-pre-line text-ink" lang="ja">
                {q.transcript}
              </p>
            </details>
          )}
        </section>

        {/* Thống kê */}
        <section className="grid grid-cols-3 gap-3">
          <Stat label="Lượt làm" value={q.stats.attempts || '—'} />
          <Stat
            label="Tỉ lệ đúng"
            value={rate === undefined ? '—' : `${rate}%`}
            hint={rate === undefined ? 'Chưa có dữ liệu' : rate < 40 ? 'Khó hơn dự kiến' : rate > 90 ? 'Dễ — cân nhắc đổi đáp án nhiễu' : 'Phù hợp'}
            tone={rateTone(rate)}
          />
          <Stat label="Đã dùng trong" value={q.stats.used ? `${q.stats.used} đề / bài` : '—'} />
        </section>

        <dl className="grid grid-cols-1 gap-x-4 gap-y-2 text-[13px] sm:grid-cols-[130px_1fr]">
          {meta.map(([k, v]) => (
            <div key={k} className="contents">
              <dt className="text-ink-muted">{k}</dt>
              <dd className="min-w-0 break-words text-ink">{v}</dd>
            </div>
          ))}
        </dl>

        {/* Quy trình duyệt */}
        {editable && (
          <div className="flex flex-wrap justify-end gap-2 border-t border-line pt-4">
            {q.status === 'draft' && (
              <Button icon={Send} onClick={() => doStatus('review', 'submit', 'Đã gửi duyệt')}>
                Gửi duyệt
              </Button>
            )}
            {q.status === 'review' && approver && (
              <>
                <Button icon={Undo2} tone="warning" variant="outline" onClick={returnBack}>
                  Trả lại
                </Button>
                <Button icon={CheckCircle2} tone="success" onClick={() => doStatus('approved', 'approve', 'Đã duyệt — câu được dùng trong đề')}>
                  Duyệt
                </Button>
              </>
            )}
            {q.status === 'approved' && approver && (
              <Button icon={Archive} tone="danger" variant="soft" onClick={retire}>
                Ngừng dùng
              </Button>
            )}
            {q.status === 'retired' && approver && (
              <Button icon={RotateCcw} variant="outline" onClick={() => doStatus('review', 'reopen', 'Đã mở lại — chờ duyệt')}>
                Mở lại (chờ duyệt)
              </Button>
            )}
          </div>
        )}
      </div>
      {confirmDialog}
    </Drawer>
  );
}

function Stat({ label, value, hint, tone }: { label: string; value: ReactNode; hint?: string; tone?: string }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-line p-3">
      <p className="text-xs text-ink-muted">{label}</p>
      <p className={cn('tabular mt-1 text-lg font-semibold', tone === 'danger' ? 'text-danger-600' : tone === 'warning' ? 'text-warning-700' : tone === 'success' ? 'text-success-700' : 'text-ink')}>{value}</p>
      {hint && <p className="mt-0.5 text-xs text-ink-muted">{hint}</p>}
    </div>
  );
}
