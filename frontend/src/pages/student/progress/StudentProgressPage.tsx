import { useState } from 'react';
import { CalendarCheck, NotebookPen, TrendingUp, Trophy } from 'lucide-react';
import { EmptyState, PageHeader, StatCard } from '@/components/common';
import { AssignmentsSection, AttendanceSection, ClassPills, CurriculumProgress, ExamsSection } from '@/features/portal/components/LearningView';
import { useStudentId, useStudentLearning } from '@/features/portal/usePortal';

/** Cổng HV › Tiến độ học tập: theo lộ trình, chuyên cần, điểm bài tập & thi của từng lớp */
export default function StudentProgressPage() {
  const studentId = useStudentId();
  const list = useStudentLearning(studentId).filter((x) => x.stats);
  const [classId, setClassId] = useState<string>();
  const cur = list.find((x) => x.cls.id === classId) ?? list[0];

  if (!cur?.stats) return <EmptyState icon={TrendingUp} title="Bạn chưa có lớp học nào" />;
  const st = cur.stats;

  return (
    <>
      <PageHeader title="Tiến độ học tập" subtitle={`${cur.cls.courseLabel} · cập nhật theo điểm danh và điểm số giáo viên nhập`} />
      <ClassPills list={list} value={cur.cls.id} onChange={setClassId} />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Chuyên cần" value={st.attendance.rate !== undefined ? `${st.attendance.rate}%` : '—'} icon={CalendarCheck} tone={st.attendance.rate !== undefined && st.attendance.rate < 80 ? 'danger' : 'success'} hint={`${st.attendance.sessions} buổi đã học`} />
        <StatCard label="Bài tập đã nộp" value={`${st.assignments.submitted}/${st.assignments.due}`} icon={NotebookPen} tone="info" hint={st.assignments.late ? `${st.assignments.late} bài nộp muộn` : undefined} />
        <StatCard label="Điểm bài tập TB" value={st.assignments.avgPct !== undefined ? `${st.assignments.avgPct}%` : '—'} icon={TrendingUp} tone="primary" />
        <StatCard label="Điểm thi TB" value={st.exams.avgPct !== undefined ? `${st.exams.avgPct}%` : '—'} icon={Trophy} tone="warning" />
      </div>

      {st.flags.filter((f) => f.key !== 'tuition').length > 0 && (
        <div className="mb-4 rounded-xl border border-warning-100 bg-warning-50 px-4 py-3 text-[13px] text-warning-700">
          <b>Cần cố gắng:</b> {st.flags.filter((f) => f.key !== 'tuition').map((f) => f.label).join(' · ')}
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="flex flex-col gap-4">
          <CurriculumProgress cls={cur.cls} />
          <AttendanceSection stats={st} />
        </div>
        <div className="flex flex-col gap-4">
          <ExamsSection stats={st} />
          <AssignmentsSection stats={st} />
        </div>
      </div>
    </>
  );
}
