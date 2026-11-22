import { useParams } from 'react-router-dom';
import { useClassStore } from '@/features/academic/classes/classStore';
import { ExamResultsBoard } from '@/features/academic/classes/exams/ExamResultsBoard';

/** Cổng GV › Lớp của tôi › Kết quả thi: nhập điểm thi giấy / vắng thi, sửa điểm online, công bố kết quả (ND-06) */
export default function TeacherExamResultsPage() {
  const { id = '', examId = '' } = useParams();
  const code = useClassStore((s) => s.classes.find((c) => c.id === id)?.code);
  return (
    <ExamResultsBoard
      classId={id}
      examId={examId}
      asTeacher
      backUrl={`/teacher/classes/${id}`}
      breadcrumb={[{ title: 'Lớp của tôi', href: '/teacher/classes' }, { title: code ?? 'Lớp', href: `/teacher/classes/${id}` }, { title: 'Kết quả thi' }]}
    />
  );
}
