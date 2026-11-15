import dayjs from 'dayjs';
import type { ExamResult, Submission, SubmissionFile } from '@/features/academic/classes/types';
import { MOCK_ASSIGNMENTS, MOCK_CLASSES, MOCK_SESSIONS } from './classes';
import { MOCK_ENROLLMENTS } from './students';

/**
 * Dữ liệu giả — Bài nộp bài tập + kết quả thi của học viên.
 * Bài nộp: ~85% nộp, một số nộp muộn, bài quá hạn phần lớn đã chấm.
 * Kết quả thi: sinh cho các buổi thi đã diễn ra; vài học viên vắng thi.
 */

const TEACHER_BY_CLASS = (classId: string) => MOCK_CLASSES.find((c) => c.id === classId)?.teacherIds[0] === 't-sato' ? 'Sato Yuki' : 'Giáo viên lớp';
const COMMENTS = ['Làm tốt, chú ý trợ từ は / が.', 'Phát âm rõ, cần luyện thêm trường âm.', 'Sai 2 câu chia thể — xem lại bảng chia động từ.', 'Rất tốt!', 'Bài làm cẩu thả, nộp lại phần 3.', 'Kanji viết đúng thứ tự nét, tốt.'];

/** Học viên của lớp (trừ người đã nghỉ) — theo thứ tự ghi danh */
const studentsOf = (classId: string) => MOCK_ENROLLMENTS.filter((e) => e.classId === classId && e.status !== 'dropped' && e.status !== 'transferred').map((e) => e.studentId);

function filesFor(seed: number, title: string): SubmissionFile[] {
  const base = title.replace(/^Bài tập:\s*/, '').slice(0, 30);
  switch (seed % 4) {
    case 0:
      return [{ name: `${base} - bài làm.pdf`, kind: 'pdf', url: '/files/demo/bai-lam.pdf', size: 380_000 + seed * 911 }];
    case 1:
      return [
        { name: 'Ảnh chụp vở trang 1.jpg', kind: 'image', url: '/files/demo/anh-1.jpg', size: 1_200_000 },
        { name: 'Ảnh chụp vở trang 2.jpg', kind: 'image', url: '/files/demo/anh-2.jpg', size: 1_050_000 },
      ];
    case 2:
      return [
        { name: `${base}.docx`, kind: 'doc', url: '/files/demo/bai-lam.docx', size: 64_000 },
        { name: 'Ghi âm luyện nói.m4a', kind: 'audio', url: '/files/demo/ghi-am.m4a', size: 2_400_000 },
      ];
    default:
      return [{ name: 'Bài làm trên Google Docs', kind: 'link', url: 'https://docs.google.com/document/d/demo' }];
  }
}

export const MOCK_SUBMISSIONS: Submission[] = MOCK_ASSIGNMENTS.flatMap((a, ai) => {
  const due = dayjs(a.dueDate).endOf('day');
  const closed = due.isBefore(dayjs());
  return studentsOf(a.classId).flatMap((studentId, j): Submission[] => {
    const seed = ai * 7 + j;
    // Chưa nộp: ~15% bài đã đóng, ~50% bài còn hạn
    if (closed ? seed % 7 === 3 : seed % 2 === 0) return [];
    const late = closed && seed % 5 === 2;
    const submittedAt = (late ? due.add(1 + (seed % 2), 'day') : dayjs(a.assignedAt).add(1 + (seed % 3), 'day')).hour(20 + (seed % 3)).minute((seed * 13) % 60);
    // Trắc nghiệm: hệ thống tự chấm ngay khi nộp
    if (a.type === 'quiz' && a.quiz) {
      const total = a.quiz.questionCount;
      const correct = Math.min(total, Math.round(total * (0.45 + ((seed * 37) % 55) / 100)));
      return [
        {
          id: `SUB-${a.id}-${studentId}`,
          assignmentId: a.id,
          studentId,
          submittedAt: (submittedAt.isAfter(dayjs()) ? dayjs().subtract(2, 'hour') : submittedAt).toISOString(),
          files: [],
          quiz: { correct, total, attempts: 1 + (seed % a.quiz.attempts), durationMinutes: 8 + (seed % 16) },
          score: Math.round((correct / total) * a.maxScore * 4) / 4,
          gradedAt: submittedAt.toISOString(),
          gradedBy: 'Hệ thống (tự chấm)',
        },
      ];
    }
    const graded = closed && seed % 6 !== 0;
    const score = graded ? Math.min(10, 5 + ((seed * 3) % 11) / 2) : undefined;
    return [
      {
        id: `SUB-${a.id}-${studentId}`,
        assignmentId: a.id,
        studentId,
        submittedAt: (submittedAt.isAfter(dayjs()) ? dayjs().subtract(2, 'hour') : submittedAt).toISOString(),
        files: filesFor(seed, a.title),
        content: seed % 3 === 0 ? 'Em nộp bài ạ. Câu 4 em chưa chắc lắm.' : undefined,
        score,
        comment: graded ? COMMENTS[seed % COMMENTS.length] : undefined,
        gradedAt: graded ? due.add(2, 'day').toISOString() : undefined,
        gradedBy: graded ? TEACHER_BY_CLASS(a.classId) : undefined,
      },
    ];
  });
});

/** 3 phần thi kiểu JLPT, tổng 100 */
const SECTIONS = [
  { name: 'Từ vựng – Kanji', max: 35 },
  { name: 'Ngữ pháp – Đọc hiểu', max: 35 },
  { name: 'Nghe hiểu', max: 30 },
];

export const MOCK_EXAM_RESULTS: ExamResult[] = MOCK_SESSIONS.filter((s) => s.kind === 'exam' && s.examId && (s.status === 'completed' || s.status === 'makeup')).flatMap((s, si) =>
  studentsOf(s.classId).map((studentId, j): ExamResult => {
    const seed = si * 11 + j;
    const absent = seed % 9 === 4;
    // Phân bố điểm ~ 40–98%, một vài bạn dưới mức đạt
    const ratio = absent ? 0 : 0.4 + ((seed * 37) % 59) / 100;
    return {
      id: `RES-${s.id}-${studentId}`,
      classId: s.classId,
      examId: s.examId!,
      studentId,
      status: absent ? 'absent' : 'done',
      sections: absent ? [] : SECTIONS.map((sec, k) => ({ ...sec, score: Math.max(0, Math.min(sec.max, Math.round(sec.max * (ratio + ((k - 1) * ((seed % 5) - 2)) / 25)))) })),
      startedAt: absent ? undefined : dayjs(s.date).hour(Number(s.startTime.slice(0, 2))).minute(Number(s.startTime.slice(3))).toISOString(),
      durationMinutes: absent ? undefined : 50 + (seed % 35),
      comment: absent ? 'Vắng có phép — chờ thi bù' : undefined,
    };
  }),
);
