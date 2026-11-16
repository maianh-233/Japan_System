import { useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { MessageSquareText, TrendingUp } from 'lucide-react';
import { EmptyState, PageHeader } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { teacherNames } from '@/features/academic/classes/constants';
import { sessionTeachers } from '@/features/academic/classes/sessionUtils';
import { ChildSwitcher } from '@/features/portal/components/ChildSwitcher';
import { AssignmentsSection, ClassPills, CurriculumProgress, ExamsSection } from '@/features/portal/components/LearningView';
import { useChildren, useStudentId, useStudentLearning } from '@/features/portal/usePortal';

/** Cổng PH › Kết quả học tập: tiến độ, điểm kiểm tra, điểm bài tập, nhận xét giáo viên sau các buổi học */
export default function ParentResultsPage() {
  const studentId = useStudentId();
  const child = useChildren().find((c) => c.id === studentId);
  const list = useStudentLearning(studentId).filter((x) => x.stats && x.enrollment.status !== 'dropped');
  const sessions = useClassStore((s) => s.sessions);
  const [classId, setClassId] = useState<string>();
  const cur = list.find((x) => x.cls.id === classId) ?? list[0];

  // Nhật ký buổi học gần đây của GV (nội dung đã học, bài về nhà) — giúp PH kèm con học
  const logs = useMemo(
    () =>
      cur
        ? sessions
            .filter((s) => s.classId === cur.cls.id && s.teacherLog)
            .sort((a, b) => b.date.localeCompare(a.date))
            .slice(0, 5)
        : [],
    [sessions, cur],
  );

  return (
    <>
      <PageHeader title="Kết quả học tập" subtitle={cur ? `${child?.fullName ?? ''} · ${cur.cls.courseLabel}` : child?.fullName} />
      <ChildSwitcher />
      {!cur?.stats ? (
        <EmptyState icon={TrendingUp} title="Chưa có kết quả học tập" />
      ) : (
        <>
          <ClassPills list={list} value={cur.cls.id} onChange={setClassId} />
          <div className="grid gap-4 lg:grid-cols-2">
            <div className="flex flex-col gap-4">
              <ExamsSection stats={cur.stats} />
              <CurriculumProgress cls={cur.cls} />
            </div>
            <div className="flex flex-col gap-4">
              <section className="rounded-xl border border-line bg-surface p-4">
                <p className="mb-2 font-semibold text-ink">Giáo viên ghi sau buổi học</p>
                <ul className="flex flex-col gap-3 text-[13px]">
                  {logs.map((s) => (
                    <li key={s.id}>
                      <p className="text-xs text-ink-muted">
                        Buổi {s.no} · {dayjs(s.date).format('dd DD/MM')} · {teacherNames(sessionTeachers(s, cur.cls))}
                      </p>
                      <p className="flex gap-1.5 text-ink">
                        <MessageSquareText size={14} className="mt-0.5 shrink-0 text-primary-600" aria-hidden /> {s.teacherLog!.content}
                      </p>
                      {s.teacherLog!.homework && <p className="ml-5 text-xs text-ink-secondary">Bài về nhà: {s.teacherLog!.homework}</p>}
                    </li>
                  ))}
                  {!logs.length && <li className="text-ink-muted">Chưa có nhật ký buổi học.</li>}
                </ul>
              </section>
              <AssignmentsSection stats={cur.stats} />
            </div>
          </div>
        </>
      )}
    </>
  );
}
