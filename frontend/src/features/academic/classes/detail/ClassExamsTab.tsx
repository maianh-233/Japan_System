import { useState } from 'react';
import dayjs from 'dayjs';
import { Ban, CalendarClock, CheckCircle2, Database, FilePen, FileQuestion, Monitor, Send } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, EmptyState, StatusTag, useConfirm, useNotify } from '@/components/common';
import { USE_API } from '@/lib/api/config';
import { fmtDateTime } from '@/lib/date';
import * as learningApi from '../learningApi';
import { ExamComposer } from '../exams/ExamComposer';
import { useAuth } from '@/hooks/useAuth';
import { fmtDate } from '@/lib/date';
import { EXAM_KINDS } from '../../curriculum/constants';
import { examScope, useCurriculumStore } from '../../curriculum/curriculumStore';
import { useClassStore } from '../classStore';
import type { ClassExam, ClassInfo } from '../types';

/**
 * Tab Bài thi: các MỐC THI theo lộ trình của lớp + buổi thi tương ứng.
 * Đề & câu hỏi được tạo ở chức năng Kỳ thi (lấy từ Ngân hàng câu hỏi theo phạm vi bài).
 */
export function ClassExamsTab({ cls }: { cls: ClassInfo }) {
  const { can, canDo } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const results = useClassStore((s) => s.examResults);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const allSessions = useClassStore((s) => s.sessions);
  const sessions = allSessions.filter((x) => x.classId === cls.id && x.kind === 'exam');
  const classExams = useClassStore((s) => s.classExams);
  const { confirm, confirmDialog } = useConfirm();
  const [composing, setComposing] = useState<{ milestone: { id: string; title: string }; exam?: ClassExam } | null>(null);
  const manage = canDo('academic.classes', 'update') && cls.status !== 'cancelled';

  const act = async (e: ClassExam, action: 'publish' | 'cancel', title: string, description: string) => {
    if (!(await confirm({ title, description, tone: action === 'cancel' ? 'danger' : 'primary', confirmAction: action === 'cancel' ? 'delete' : 'send' }))) return;
    try {
      await learningApi.changeExam(e.id, action);
      notify.toast.success(action === 'publish' ? 'Đã phát hành kỳ thi' : 'Đã hủy kỳ thi');
    } catch (err) {
      notify.error({ title: (err as Error).message });
    }
  };

  /** Chế độ API: kỳ thi thật của mốc (bỏ kỳ thi đã hủy) + thao tác theo trạng thái */
  const apiActions = (exam: { id: string; title: string }, hasResults: boolean) => {
    const ce = classExams.find((x) => x.classId === cls.id && x.examId === exam.id && x.status !== 'cancelled');
    if (!ce)
      return manage ? (
        <Button icon={FileQuestion} size="sm" variant="soft" onClick={() => setComposing({ milestone: exam })}>
          Tạo kỳ thi
        </Button>
      ) : hasResults ? (
        <Button size="sm" variant="outline" icon={FileQuestion} onClick={() => navigate(`/admin/academic/classes/${cls.id}/exams/${exam.id}`)}>
          Xem kết quả
        </Button>
      ) : null;
    return (
      <div className="flex flex-wrap gap-1.5">
        {ce.status === 'draft' && manage && (
          <>
            <Button icon={FilePen} size="sm" variant="soft" onClick={() => setComposing({ milestone: exam, exam: ce })}>
              Sửa đề
            </Button>
            <Button icon={Send} size="sm" onClick={() => void act(ce, 'publish', `Phát hành "${ce.title}"?`, 'Đề sẽ chốt (không đổi câu nữa). Thi online: học viên thấy ở Lịch thi.')}>
              Phát hành
            </Button>
          </>
        )}
        {ce.status === 'published' && (
          <>
            <Button icon={FileQuestion} size="sm" variant="soft" onClick={() => navigate(`/admin/academic/classes/${cls.id}/exams/${exam.id}`)}>
              Kết quả
            </Button>
            {manage && !ce.attempts && (
              <Button icon={CalendarClock} size="sm" variant="outline" onClick={() => setComposing({ milestone: exam, exam: ce })}>
                Đổi giờ
              </Button>
            )}
          </>
        )}
        {manage && !ce.attempts && !ce.results && (
          <Button icon={Ban} iconOnly size="sm" variant="ghost" tone="danger" aria-label="Hủy kỳ thi" tooltip="Hủy kỳ thi" onClick={() => void act(ce, 'cancel', `Hủy "${ce.title}"?`, 'Có thể tạo kỳ thi khác cho mốc này.')} />
        )}
      </div>
    );
  };

  const examLine = (exam: { id: string }) => {
    const ce = classExams.find((x) => x.classId === cls.id && x.examId === exam.id && x.status !== 'cancelled');
    if (!ce) return null;
    const tag =
      ce.status === 'draft'
        ? { tone: 'warning' as const, label: 'Nháp' }
        : ce.resultsPublishedAt
          ? { tone: 'success' as const, label: 'Đã công bố kết quả' }
          : { tone: 'info' as const, label: 'Đã phát hành' };
    return (
      <p className="mt-1 flex flex-wrap items-center gap-2 text-[13px] text-ink-secondary">
        <StatusTag tone={tag.tone} icon={tag.tone === 'success' ? CheckCircle2 : tag.tone === 'warning' ? FilePen : Send} size="sm">
          {tag.label}
        </StatusTag>
        <span className="inline-flex items-center gap-1">
          <Monitor size={13} aria-hidden /> {ce.mode === 'online' ? 'Online' : 'Giấy'} · {fmtDateTime(ce.startsAt)} · {ce.durationMinutes} phút
        </span>
        <span>
          {ce.questionCount} câu / {ce.maxScore} điểm · đạt ≥ {ce.passPercent}%
        </span>
        {ce.status === 'published' && <span>· {ce.attempts} bài làm, {ce.results} kết quả</span>}
      </p>
    );
  };

  if (!curriculum?.exams.length)
    return (
      <EmptyState
        size="compact"
        icon={FileQuestion}
        title="Lộ trình của lớp chưa có mốc thi"
        description="Thêm mốc thi giữa kỳ / cuối kỳ ở Đào tạo → Lộ trình (cần tạo phiên bản mới nếu lộ trình đã phát hành)."
      />
    );

  return (
    <div className="flex flex-col gap-3">
      <p className="flex items-center gap-2 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2.5 text-[13px] text-primary-800">
        <Database size={16} className="shrink-0" aria-hidden />
        Mốc thi lấy từ lộ trình {curriculum.name} {curriculum.version}. Bấm "Tạo kỳ thi" để chọn câu hỏi từ Ngân hàng câu hỏi theo phạm vi bài.
      </p>
      <ul className="flex flex-col gap-3">
        {curriculum.exams.map((exam) => {
          const meta = EXAM_KINDS[exam.kind];
          const session = sessions.find((s) => s.examId === exam.id);
          const scope = examScope(curriculum, exam.id);
          const taken = session && (session.status === 'completed' || (session.status === 'makeup' && session.attendance));
          const mine = results.filter((r) => r.classId === cls.id && r.examId === exam.id);
          const doneRes = mine.filter((r) => r.status === 'done');
          const pct = (r: (typeof mine)[number]) => {
            const max = r.sections.reduce((n, s) => n + s.max, 0) || 100;
            return (r.sections.reduce((n, s) => n + s.score, 0) / max) * 100;
          };
          const avg = doneRes.length ? Math.round(doneRes.reduce((n, r) => n + pct(r), 0) / doneRes.length) : undefined;
          const passed = doneRes.filter((r) => pct(r) >= 60).length;
          const daysLeft = session && !taken ? dayjs(session.date).startOf('day').diff(dayjs().startOf('day'), 'day') : undefined;
          return (
            <li key={exam.id} className="flex flex-wrap items-center gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4">
              <div className="min-w-0 flex-1">
                <p className="flex flex-wrap items-center gap-2">
                  <span className="font-semibold text-ink">{exam.title}</span>
                  <StatusTag tone={meta.tone} icon={meta.icon} size="sm">
                    {meta.label}
                  </StatusTag>
                  {taken ? (
                    <StatusTag tone="success" icon={CheckCircle2} size="sm">
                      Đã thi
                    </StatusTag>
                  ) : session ? (
                    <StatusTag tone="info" icon={CalendarClock} size="sm">
                      {daysLeft! < 0 ? 'Chưa xác nhận' : daysLeft === 0 ? 'Hôm nay' : daysLeft === 1 ? 'Ngày mai' : `Còn ${daysLeft} ngày`}
                    </StatusTag>
                  ) : null}
                </p>
                <p className="mt-1 text-[13px] text-ink-secondary">
                  {scope && `Phạm vi: Bài ${scope.from}–${scope.to}`}
                  {session ? ` · Buổi ${session.no}, ${fmtDate(session.date)} ${session.startTime}–${session.endTime}` : ' · Chưa có buổi thi trên lịch (tạo lại lịch ở tab Buổi học)'}
                </p>
                {exam.note && <p className="text-xs text-ink-muted">Ghi chú ra đề: {exam.note}</p>}
                {USE_API && examLine(exam)}
                {mine.length > 0 && (
                  <p className="mt-1 text-[13px] text-ink">
                    Dự thi <strong>{doneRes.length}/{mine.length}</strong> · Điểm TB <strong>{avg ?? '—'}%</strong> · Đạt <strong>{passed}/{doneRes.length}</strong>
                  </p>
                )}
              </div>
              {USE_API && apiActions(exam, mine.length > 0)}
              {!USE_API && can('academic.classes') && cls.status !== 'cancelled' && (
                <Button
                  icon={FileQuestion}
                  size="sm"
                  variant={taken ? 'outline' : 'soft'}
                  onClick={() =>
                    taken || mine.length
                      ? navigate(`/admin/academic/classes/${cls.id}/exams/${exam.id}`)
                      : notify.info({
                          title: 'Tạo kỳ thi',
                          description: `Mở chức năng Kỳ thi với phạm vi Bài ${scope?.from}–${scope?.to} để chọn câu hỏi từ Ngân hàng câu hỏi (sẽ làm ở module Kỳ thi).`,
                        })
                  }
                >
                  {taken || mine.length ? 'Xem kết quả' : 'Tạo kỳ thi'}
                </Button>
              )}
            </li>
          );
        })}
      </ul>
      {composing && <ExamComposer cls={cls} milestone={composing.milestone} exam={composing.exam} open onClose={() => setComposing(null)} />}
      {confirmDialog}
    </div>
  );
}
