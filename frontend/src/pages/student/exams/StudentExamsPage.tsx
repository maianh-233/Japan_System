import { useMemo, useState } from "react";
import { Progress, Tabs } from "antd";
import dayjs from "dayjs";
import {
  CalendarClock,
  MapPin,
  MessageSquareText,
  Play,
  Timer,
  Trophy,
  Video,
} from "lucide-react";
import {
  Button,
  EmptyState,
  PageHeader,
  Select,
  StatusTag,
} from "@/components/common";
import { useClassStore } from "@/features/academic/classes/classStore";
import {
  notStarted,
  sessionRoom,
} from "@/features/academic/classes/sessionUtils";
import { useCurriculumStore } from "@/features/academic/curriculum/curriculumStore";
import { Q_LEVELS } from "@/features/academic/questions/constants";
import { useQuestionStore } from "@/features/academic/questions/questionStore";
import type { Question, QLevel } from "@/features/academic/questions/types";
import {
  QuizRunner,
  type QuizResult,
} from "@/features/portal/components/QuizRunner";
import { ExamBoard } from "@/features/portal/components/ExamBoard";
import { USE_API } from "@/lib/api/config";
import { levelOf, mockExam } from "@/features/portal/quiz";
import {
  activeOf,
  shortCode,
  useStudentId,
  useStudentLearning,
} from "@/features/portal/usePortal";
import { cn } from "@/lib/cn";
import { fmtDate, fromNow } from "@/lib/date";

interface Practice {
  at: string;
  level: QLevel;
  correct: number;
  total: number;
  durationSec: number;
}

/**
 * Cổng HV › Bài thi
 *  - Lịch thi: mốc thi giữa kỳ / cuối kỳ của lộ trình (đề chính thức do trung tâm tạo từ Ngân hàng câu hỏi, làm vào giờ thi)
 *  - Kết quả: điểm từng phần + nhận xét GV
 *  - Luyện thi: đề ngẫu nhiên đếm giờ, chấm ngay, xem đáp án
 */
export default function StudentExamsPage() {
  const studentId = useStudentId();
  const learning = useStudentLearning(studentId);
  const sessions = useClassStore((s) => s.sessions);
  const curricula = useCurriculumStore((s) => s.curricula);
  const questions = useQuestionStore((s) => s.questions);
  const active = activeOf(learning);
  const defaultLevel = levelOf(
    curricula.find((c) => c.id === active[0]?.cls.curriculumId)?.level,
  );
  const [level, setLevel] = useState<QLevel>(defaultLevel);
  const [size, setSize] = useState(15);
  const [running, setRunning] = useState<Question[] | null>(null);
  const [history, setHistory] = useState<Practice[]>([]);

  const upcoming = useMemo(
    () =>
      active
        .flatMap(({ cls }) =>
          sessions
            .filter(
              (s) =>
                s.classId === cls.id &&
                s.kind === "exam" &&
                s.status !== "cancelled" &&
                notStarted(s),
            )
            .map((s) => ({ s, cls })),
        )
        .sort((a, b) => a.s.date.localeCompare(b.s.date)),
    [active, sessions],
  );
  const results = learning.flatMap(({ cls, stats }) =>
    (stats?.exams.list ?? []).map((e) => ({ ...e, cls })),
  );

  const scopeOf = (curriculumId: string | undefined, examId?: string) => {
    const cur = curricula.find((c) => c.id === curriculumId);
    const ex = cur?.exams.find((e) => e.id === examId);
    if (!cur || !ex) return undefined;
    const end = cur.lessons.findIndex((l) => l.id === ex.afterLessonId);
    const prev = cur.exams
      .filter(
        (e) => cur.lessons.findIndex((l) => l.id === e.afterLessonId) < end,
      )
      .map((e) => cur.lessons.findIndex((l) => l.id === e.afterLessonId));
    const start = prev.length ? Math.max(...prev) + 1 : 0;
    return {
      title: ex.title,
      range: `Bài ${start + 1} – ${end + 1}`,
      note: ex.note,
    };
  };

  const start = () =>
    setRunning(mockExam(questions, level, size, `${studentId}-${Date.now()}`));

  return (
    <>
      <PageHeader
        title="Bài thi"
        subtitle="Lịch thi giữa kỳ / cuối kỳ, kết quả từng phần và đề luyện thi đếm giờ."
      />
      {USE_API ? (
        <ExamBoard studentId={studentId} canTake />
      ) : (
        <Tabs
          items={[
            {
              key: "upcoming",
              label: `Lịch thi (${upcoming.length})`,
              children: upcoming.length ? (
                <div className="grid gap-3 md:grid-cols-2">
                  {upcoming.map(({ s, cls }) => {
                    const scope = scopeOf(cls.curriculumId, s.examId);
                    const days = dayjs(s.date).diff(
                      dayjs().startOf("day"),
                      "day",
                    );
                    return (
                      <article
                        key={s.id}
                        className="rounded-xl border border-danger-100 bg-surface p-4"
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div>
                            <p className="font-semibold text-ink">
                              {scope?.title ?? "Bài thi"}
                            </p>
                            <p className="text-[13px] text-ink-secondary">
                              {shortCode(cls)} · {cls.courseLabel}
                            </p>
                          </div>
                          <StatusTag
                            tone={days <= 3 ? "danger" : "warning"}
                            icon={CalendarClock}
                            size="sm"
                          >
                            {days === 0 ? "Hôm nay" : `Còn ${days} ngày`}
                          </StatusTag>
                        </div>
                        <p className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-[13px] text-ink-secondary">
                          <span className="inline-flex items-center gap-1">
                            <Timer size={14} aria-hidden />{" "}
                            {dayjs(s.date).format("dddd DD/MM")} {s.startTime}–
                            {s.endTime}
                          </span>
                          <span className="inline-flex items-center gap-1">
                            {cls.online ? (
                              <Video size={14} aria-hidden />
                            ) : (
                              <MapPin size={14} aria-hidden />
                            )}{" "}
                            {cls.online
                              ? "Thi online"
                              : `Phòng ${sessionRoom(s, cls) ?? "—"}`}
                          </span>
                        </p>
                        {scope && (
                          <p className="mt-2 rounded-lg bg-canvas px-3 py-2 text-[13px]">
                            Phạm vi: <b>{scope.range}</b>
                            {scope.note && (
                              <span className="block text-xs text-ink-secondary">
                                {scope.note}
                              </span>
                            )}
                          </p>
                        )}
                      </article>
                    );
                  })}
                </div>
              ) : (
                <EmptyState
                  icon={CalendarClock}
                  title="Chưa có lịch thi sắp tới"
                />
              ),
            },
            {
              key: "results",
              label: `Kết quả (${results.length})`,
              children: results.length ? (
                <div className="flex flex-col gap-3">
                  {results.map((r) => (
                    <article
                      key={r.result.id}
                      className="rounded-xl border border-line bg-surface p-4"
                    >
                      <div className="flex flex-wrap items-start justify-between gap-2">
                        <div>
                          <p className="font-semibold text-ink">{r.title}</p>
                          <p className="text-xs text-ink-muted">
                            {shortCode(r.cls)}
                            {r.result.startedAt &&
                              ` · ${fmtDate(r.result.startedAt)}`}
                          </p>
                        </div>
                        {r.result.status === "absent" ? (
                          <StatusTag
                            tone="neutral"
                            icon={CalendarClock}
                            size="sm"
                          >
                            Vắng thi
                          </StatusTag>
                        ) : (
                          <span
                            className={cn(
                              "tabular rounded-lg px-2.5 py-1 text-sm font-bold",
                              r.pct >= 60
                                ? "bg-success-50 text-success-700"
                                : "bg-danger-50 text-danger-700",
                            )}
                          >
                            {r.total}/{r.max} · {r.pct}%
                          </span>
                        )}
                      </div>
                      {r.result.status === "done" && (
                        <div className="mt-3 flex flex-col gap-1.5 text-[13px]">
                          {r.result.sections.map((x) => (
                            <div
                              key={x.name}
                              className="grid grid-cols-[130px_1fr_60px] items-center gap-2"
                            >
                              <span className="truncate text-ink-secondary">
                                {x.name}
                              </span>
                              <Progress
                                percent={Math.round((x.score / x.max) * 100)}
                                showInfo={false}
                                size="small"
                                status={
                                  x.score / x.max < 0.5 ? "exception" : "normal"
                                }
                              />
                              <span className="tabular text-right">
                                {x.score}/{x.max}
                              </span>
                            </div>
                          ))}
                        </div>
                      )}
                      {r.result.comment && (
                        <p className="mt-3 flex gap-2 rounded-lg bg-primary-50 px-3 py-2 text-[13px] text-primary-800">
                          <MessageSquareText
                            size={16}
                            className="mt-0.5 shrink-0"
                            aria-hidden
                          />{" "}
                          {r.result.comment}
                        </p>
                      )}
                    </article>
                  ))}
                </div>
              ) : (
                <EmptyState icon={Trophy} title="Chưa có kết quả thi" />
              ),
            },
            {
              key: "practice",
              label: "Luyện thi",
              children: (
                <div className="grid gap-4 lg:grid-cols-[360px_1fr]">
                  <section className="rounded-xl border border-line bg-surface p-4">
                    <p className="font-semibold text-ink">
                      Đề luyện ngẫu nhiên
                    </p>
                    <p className="mb-3 text-[13px] text-ink-secondary">
                      Trộn từ vựng, Kanji, ngữ pháp, đọc hiểu theo cấp độ — mỗi
                      lần một đề khác. 1 phút / câu.
                    </p>
                    <div className="flex flex-col gap-2">
                      <Select
                        value={level}
                        onChange={(v) => v && setLevel(v as QLevel)}
                        options={Q_LEVELS.map((l) => ({
                          value: l,
                          label: `JLPT ${l}`,
                        }))}
                        allowClear={false}
                        showSearch={false}
                        aria-label="Cấp độ"
                      />
                      <Select
                        value={size}
                        onChange={(v) => v && setSize(v as number)}
                        options={[10, 15, 20].map((n) => ({
                          value: n,
                          label: `${n} câu · ${n} phút`,
                        }))}
                        allowClear={false}
                        showSearch={false}
                        aria-label="Số câu"
                      />
                      <Button icon={Play} onClick={start}>
                        Bắt đầu
                      </Button>
                    </div>
                  </section>
                  <section>
                    <p className="mb-2 font-semibold text-ink">
                      Lần luyện gần đây
                    </p>
                    <ul className="flex flex-col divide-y divide-line rounded-xl border border-line bg-surface">
                      {history.map((h, i) => (
                        <li
                          key={i}
                          className="flex items-center justify-between px-4 py-2.5 text-[13px]"
                        >
                          <span>
                            JLPT {h.level} · {fromNow(h.at)}
                          </span>
                          <span className="tabular font-semibold">
                            {h.correct}/{h.total} ·{" "}
                            {Math.round((h.correct / h.total) * 100)}%
                          </span>
                        </li>
                      ))}
                      {!history.length && (
                        <li className="px-4 py-6 text-center text-ink-muted">
                          Chưa luyện lần nào trong phiên này.
                        </li>
                      )}
                    </ul>
                  </section>
                </div>
              ),
            },
          ]}
        />
      )}
      <QuizRunner
        open={Boolean(running)}
        title={`Luyện thi JLPT ${level}`}
        questions={running ?? []}
        timeLimit={running?.length}
        onClose={() => setRunning(null)}
        onSubmit={(r: QuizResult) =>
          setHistory((h) => [
            {
              at: new Date().toISOString(),
              level,
              correct: r.correct,
              total: r.total,
              durationSec: r.durationSec,
            },
            ...h,
          ])
        }
      />
    </>
  );
}
