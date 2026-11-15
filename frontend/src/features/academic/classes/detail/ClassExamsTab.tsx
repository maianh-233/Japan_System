import dayjs from 'dayjs';
import { CalendarClock, CheckCircle2, Database, FileQuestion } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, EmptyState, StatusTag, useNotify } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { fmtDate } from '@/lib/date';
import { EXAM_KINDS } from '../../curriculum/constants';
import { examScope, useCurriculumStore } from '../../curriculum/curriculumStore';
import { useClassStore } from '../classStore';
import type { ClassInfo } from '../types';

/**
 * Tab Bài thi: các MỐC THI theo lộ trình của lớp + buổi thi tương ứng.
 * Đề & câu hỏi được tạo ở chức năng Kỳ thi (lấy từ Ngân hàng câu hỏi theo phạm vi bài).
 */
export function ClassExamsTab({ cls }: { cls: ClassInfo }) {
  const { can } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const results = useClassStore((s) => s.examResults);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const allSessions = useClassStore((s) => s.sessions);
  const sessions = allSessions.filter((x) => x.classId === cls.id && x.kind === 'exam');

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
                {mine.length > 0 && (
                  <p className="mt-1 text-[13px] text-ink">
                    Dự thi <strong>{doneRes.length}/{mine.length}</strong> · Điểm TB <strong>{avg ?? '—'}%</strong> · Đạt <strong>{passed}/{doneRes.length}</strong>
                  </p>
                )}
              </div>
              {can('academic.classes') && cls.status !== 'cancelled' && (
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
    </div>
  );
}
