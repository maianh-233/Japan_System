import dayjs from 'dayjs';
import type { CareLog, ClassAnnouncement, ReviewCriterion, StudentEvaluation, TeacherReview } from '@/features/academic/classes/types';
import { MOCK_CLASSES } from './classes';
import { MOCK_SESSIONS } from './attendance';
import { MOCK_ENROLLMENTS } from './students';

/**
 * Dữ liệu giả — đánh giá giáo viên, thông báo đã gửi, nhật ký chăm sóc học viên.
 * Đánh giá: đợt giữa kỳ mở khi lớp học quá nửa số buổi, đợt cuối kỳ khi lớp kết thúc; ~75% học viên trả lời.
 */

const ago = (days: number, h = 9) => dayjs().subtract(days, 'day').hour(h).minute(0).second(0).toISOString();

const GOOD = [
  'Cô giảng dễ hiểu, ví dụ gần gũi, em tiến bộ rõ phần ngữ pháp.',
  'Thầy nhiệt tình, chữa bài kỹ từng lỗi.',
  'Lớp vui, có nhiều hoạt động luyện nói.',
  'Giải thích kanji theo bộ rất dễ nhớ.',
  'Cô luôn đúng giờ, chuẩn bị bài chu đáo.',
];
const MIXED = ['Tốc độ hơi nhanh với em, mong cô dành thêm thời gian ôn lại.', 'Mong có thêm bài luyện nghe trên lớp.', 'Phần bài tập về nhà hơi nhiều.'];
const BAD = ['Thầy hay đến muộn 5–10 phút.', 'Giảng nhanh, ít thời gian hỏi đáp.'];

const clamp = (n: number) => Math.max(1, Math.min(5, Math.round(n)));

function reviewsFor(classId: string, period: 'mid' | 'final', baseDaysAgo: number): TeacherReview[] {
  const cls = MOCK_CLASSES.find((c) => c.id === classId)!;
  const students = MOCK_ENROLLMENTS.filter((e) => e.classId === classId && e.status !== 'dropped').map((e) => e.studentId);
  return cls.teacherIds.flatMap((teacherId, ti) =>
    students
      .filter((_, i) => (i + ti) % 4 !== 3) // ~75% trả lời
      .map((studentId, i): TeacherReview => {
        // Mỗi GV có "mặt bằng" riêng; GV Long hay muộn → điểm đúng giờ thấp
        const base = teacherId === 't-long' ? 3.8 : teacherId === 't-sato' ? 4.6 : 4.3;
        const wobble = ((i * 7 + ti * 3) % 5) - 2; // -2..2
        const overall = clamp(base + wobble * 0.45);
        const ratings: Record<ReviewCriterion, number> = {
          method: clamp(overall + (i % 3 === 0 ? -1 : 0)),
          clarity: clamp(overall + (i % 4 === 1 ? -1 : 0)),
          enthusiasm: clamp(overall + (i % 2 ? 1 : 0)),
          punctuality: clamp(teacherId === 't-long' ? overall - 1 : overall + 1),
        };
        const pool = overall >= 4 ? GOOD : overall === 3 ? MIXED : teacherId === 't-long' ? BAD : MIXED;
        return {
          id: `RV-${classId}-${period}-${teacherId}-${studentId}`,
          classId,
          teacherId,
          studentId,
          period,
          overall,
          ratings,
          comment: i % 3 === 2 ? undefined : pool[(i + ti) % pool.length],
          anonymous: i % 3 !== 0,
          createdAt: ago(baseDaysAgo - (i % 5), 20),
        };
      }),
  );
}

export const MOCK_TEACHER_REVIEWS: TeacherReview[] = MOCK_CLASSES.flatMap((c) => {
  if (c.status === 'enrolling' || c.status === 'cancelled') return [];
  const mine = MOCK_SESSIONS.filter((s) => s.classId === c.id);
  const done = mine.filter((s) => s.attendance).length;
  const out: TeacherReview[] = [];
  if (done >= mine.length / 2 || c.status === 'finished') {
    const mid = mine.filter((s) => s.attendance)[Math.floor(mine.length / 2) - 1];
    out.push(...reviewsFor(c.id, 'mid', mid ? Math.max(1, dayjs().diff(dayjs(mid.date), 'day') - 1) : 10));
  }
  if (c.status === 'finished') out.push(...reviewsFor(c.id, 'final', Math.max(1, dayjs().diff(dayjs(c.actualEndDate), 'day') - 2)));
  return out;
});

export const MOCK_ANNOUNCEMENTS: ClassAnnouncement[] = MOCK_CLASSES.filter((c) => c.status === 'ongoing').flatMap((c, i): ClassAnnouncement[] => {
  const n = MOCK_ENROLLMENTS.filter((e) => e.classId === c.id && e.status === 'studying').length;
  return [
    {
      id: `AN-${c.id}-1`,
      classId: c.id,
      title: 'Chào mừng khai giảng',
      content: `Chào mừng các bạn đến với lớp ${c.code}. Lịch học, tài liệu và bài tập xem trên Cổng học viên. Mọi thắc mắc liên hệ Giáo vụ.`,
      audience: ['student', 'parent'],
      channels: ['portal', 'zalo'],
      recipients: n * 2,
      readCount: Math.round(n * 1.7),
      sentBy: 'Giáo vụ',
      sentAt: dayjs(c.actualStartDate ?? c.plannedStartDate).subtract(1, 'day').hour(10).toISOString(),
    },
    ...(i % 2 === 0
      ? [
          {
            id: `AN-${c.id}-2`,
            classId: c.id,
            title: 'Nhắc đóng học phí đợt 2',
            content: `Lớp ${c.code}: các bạn còn nợ học phí vui lòng hoàn tất trước hạn. Có thể chuyển khoản / VietQR trên Cổng học viên.`,
            audience: ['student', 'parent'] as const,
            channels: ['portal', 'zalo', 'email'] as const,
            debtorsOnly: true,
            recipients: Math.max(1, Math.round(n / 3)),
            readCount: Math.max(1, Math.round(n / 4)),
            sentBy: 'Kế toán',
            sentAt: ago(6, 9),
          } satisfies ClassAnnouncement,
        ]
      : []),
  ];
});

/** 1 lần liên hệ mẫu cho học viên vắng nhiều nhất ở vài lớp */
export const MOCK_CARE_LOGS: CareLog[] = MOCK_CLASSES.filter((c) => c.status === 'ongoing')
  .slice(0, 3)
  .flatMap((c) => {
    const st = MOCK_ENROLLMENTS.find((e) => e.classId === c.id && e.status === 'studying');
    return st ? [{ id: `CL-${c.id}`, classId: c.id, studentId: st.studentId, channel: 'call' as const, note: 'Đã gọi phụ huynh — học viên ốm 1 tuần, tuần sau đi học lại.', by: 'Giáo vụ', at: ago(3, 15) }] : [];
  });

/** Nhận xét cuối khóa mẫu: vài học viên đã học xong ở các lớp đã kết thúc (GV viết khi lớp kết thúc) */
const EVAL_SAMPLES: Pick<StudentEvaluation, 'skills' | 'attitude' | 'strengths' | 'weaknesses' | 'recommendation' | 'advice'>[] = [
  {
    skills: { vocab: 4, grammar: 5, reading: 4, listening: 3, speaking: 3 },
    attitude: 5,
    strengths: 'Nắm ngữ pháp chắc, làm bài tập đầy đủ, hỏi bài chủ động.',
    weaknesses: 'Nghe hiểu tốc độ thường còn chậm, ngại nói trước lớp.',
    recommendation: 'next_level',
    advice: 'Đủ sức lên khóa tiếp theo. Nên xếp lớp có GV Nhật để tăng phản xạ nghe – nói.',
  },
  {
    skills: { vocab: 3, grammar: 2, reading: 3, listening: 2, speaking: 3 },
    attitude: 3,
    strengths: 'Phát âm tốt, giao tiếp mạnh dạn.',
    weaknesses: 'Vắng nhiều giữa khóa, hổng ngữ pháp các bài cuối; điểm thi cuối kỳ dưới trung bình.',
    recommendation: 'repeat',
    advice: 'Tư vấn học lại khóa hoặc lớp ôn tập ngắn trước khi lên trình độ mới.',
  },
];

export const MOCK_EVALUATIONS: StudentEvaluation[] = MOCK_CLASSES.filter((c) => c.status === 'finished')
  .slice(0, 2)
  .flatMap((c) =>
    MOCK_ENROLLMENTS.filter((e) => e.classId === c.id && e.status === 'completed')
      .slice(0, 2)
      .map((e, i) => ({
        id: `EV-${c.id}-${e.studentId}`,
        classId: c.id,
        studentId: e.studentId,
        teacherId: c.teacherIds[0],
        author: 'Giáo viên lớp',
        ...EVAL_SAMPLES[i % EVAL_SAMPLES.length],
        createdAt: ago(10 + i),
        updatedAt: ago(10 + i),
      })),
  );
