import dayjs from 'dayjs';
import { teacherName, totalSessionsOf } from '@/features/academic/classes/constants';
import { generateSessions } from '@/features/academic/classes/sessionGenerator';
import type { Assignment, ClassInfo, ClassSession, SessionLog, Weekday } from '@/features/academic/classes/types';
import { MOCK_CURRICULA } from './curricula';

/**
 * Dữ liệu giả — Lớp học, buổi học (tự sinh), bài tập.
 * Tình huống: đang học, khai giảng trễ, chờ khai giảng chưa đủ HV, quá ngày dự kiến chưa khai giảng,
 * tạm dừng, bị hủy, đã kết thúc; lớp Việt + Nhật (2 GV) online.
 */

const d = (days: number) => dayjs().add(days, 'day').format('YYYY-MM-DD');
const ago = (days: number) => dayjs().subtract(days, 'day').hour(9).minute(0).second(0).toISOString();

type Seed = Omit<ClassInfo, 'plannedEndDate' | 'createdAt'> & { plannedEndDate?: string };

const seeds: Seed[] = [
  // ---- Cầu Giấy
  { id: 'C-N3K12', courseId: 'CRS-03', curriculumId: 'CUR-N3-V1', code: 'GVV_N3-K12', name: 'JLPT N3 K12 — Tối 2-4-6', courseLabel: 'JLPT N3', branchId: 'cg', teacherIds: ['t-ha'], assistantIds: ['ta-linh'], room: 'P.301', plannedStartDate: d(-60), actualStartDate: d(-53), weekdays: [1, 3, 5], startTime: '18:00', endTime: '20:00', totalHours: 90, minStudents: 8, maxStudents: 18, fee: 6_800_000, status: 'ongoing', note: 'Khai giảng trễ 1 tuần do chưa đủ 8 học viên.' },
  { id: 'C-KIDS05', courseId: 'CRS-06', curriculumId: 'CUR-KIDS', code: 'GVV_KIDS-K05', name: 'Thiếu nhi K05 — Sáng cuối tuần', courseLabel: 'Tiếng Nhật thiếu nhi', branchId: 'cg', teacherIds: ['t-anh'], assistantIds: ['ta-linh'], room: 'P.205', plannedStartDate: d(-45), actualStartDate: d(-45), weekdays: [6, 7], startTime: '09:00', endTime: '11:00', totalHours: 54, minStudents: 6, maxStudents: 15, fee: 4_500_000, status: 'ongoing' },
  { id: 'C-N4K15', courseId: 'CRS-02', curriculumId: 'CUR-N4-V2', code: 'GVV_N4-K15', name: 'JLPT N4 K15 — Tối 3-5', courseLabel: 'JLPT N4', branchId: 'cg', teacherIds: ['t-long'], room: 'P.302', plannedStartDate: d(-30), actualStartDate: d(-30), weekdays: [2, 4], startTime: '18:00', endTime: '20:00', totalHours: 96, minStudents: 8, maxStudents: 20, fee: 5_900_000, status: 'ongoing' },
  { id: 'C-N5K18', courseId: 'CRS-01', curriculumId: 'CUR-N5-V1', code: 'GVV_N5-K18', name: 'JLPT N5 K18 — Tối 2-4-6', courseLabel: 'JLPT N5 cơ bản', branchId: 'cg', teacherIds: ['t-anh'], room: 'P.201', plannedStartDate: d(-260), actualStartDate: d(-260), actualEndDate: d(-80), weekdays: [1, 3, 5], startTime: '19:30', endTime: '21:30', totalHours: 72, minStudents: 8, maxStudents: 20, fee: 4_900_000, status: 'finished' },
  { id: 'C-N4K11', courseId: 'CRS-02', curriculumId: 'CUR-N4-V2', code: 'GVV_N4-K11', name: 'JLPT N4 K11 — Tối 3-5', courseLabel: 'JLPT N4', branchId: 'cg', teacherIds: ['t-long'], room: 'P.302', plannedStartDate: d(-300), actualStartDate: d(-296), actualEndDate: d(-70), weekdays: [2, 4], startTime: '18:00', endTime: '20:00', totalHours: 96, minStudents: 8, maxStudents: 20, fee: 5_900_000, status: 'finished' },
  { id: 'C-N5K21', courseId: 'CRS-01', curriculumId: 'CUR-N5-V2', code: 'GVV_N5-K21', name: 'JLPT N5 K21 — Tối 2-4-6', courseLabel: 'JLPT N5 cơ bản', branchId: 'cg', teacherIds: ['t-anh'], room: 'P.201', plannedStartDate: d(12), weekdays: [1, 3, 5], startTime: '18:00', endTime: '20:00', totalHours: 72, minStudents: 8, maxStudents: 20, fee: 4_900_000, status: 'enrolling' },
  { id: 'C-N4K14', courseId: 'CRS-02', curriculumId: 'CUR-N4-V2', code: 'GVV_N4-K14', name: 'JLPT N4 K14 — Sáng T7-CN', courseLabel: 'JLPT N4', branchId: 'cg', teacherIds: ['t-long'], room: 'P.205', plannedStartDate: d(-20), weekdays: [6, 7], startTime: '08:00', endTime: '10:00', totalHours: 96, minStudents: 8, maxStudents: 20, fee: 5_900_000, status: 'cancelled', cancelReason: 'Sau 3 tuần chỉ có 3 học viên — chuyển sang lớp N4-K15.' },
  // Thêm ca sáng / chiều + lớp online học tại nhà (không có phòng) — trùng giờ với lớp offline
  { id: 'C-N5K22', courseId: 'CRS-01', curriculumId: 'CUR-N5-V2', code: 'GVV_N5-K22', name: 'JLPT N5 K22 — Sáng 2-4-6', courseLabel: 'JLPT N5 cơ bản', branchId: 'cg', teacherIds: ['t-anh'], room: 'P.201', plannedStartDate: d(-25), actualStartDate: d(-25), weekdays: [1, 3, 5], startTime: '08:30', endTime: '10:30', totalHours: 72, minStudents: 8, maxStudents: 20, fee: 4_900_000, status: 'ongoing' },
  { id: 'C-KAIWAC2', courseId: 'CRS-05', curriculumId: 'CUR-KAIWA', code: 'GVN_KAIWA-C2', name: 'Kaiwa C2 — Chiều 3-5', courseLabel: 'Kaiwa giao tiếp', branchId: 'cg', teacherIds: ['t-sato'], room: 'P.301', plannedStartDate: d(-35), actualStartDate: d(-35), weekdays: [2, 4], startTime: '14:00', endTime: '16:00', totalHours: 40, minStudents: 5, maxStudents: 12, fee: 5_200_000, status: 'ongoing' },
  { id: 'C-N4ON03', courseId: 'CRS-02', curriculumId: 'CUR-N4-V2', code: 'GVV_N4-ON03', name: 'JLPT N4 Online K03 — Tối 2-4-6', courseLabel: 'JLPT N4', branchId: 'cg', teacherIds: ['t-long'], online: true, meetingUrl: 'https://meet.google.com/abc-defg-hij', plannedStartDate: d(-28), actualStartDate: d(-28), weekdays: [1, 3, 5], startTime: '19:30', endTime: '21:30', totalHours: 96, minStudents: 6, maxStudents: 20, fee: 5_400_000, status: 'ongoing' },
  { id: 'C-N3ON14', courseId: 'CRS-03', curriculumId: 'CUR-N3-V1', code: 'GVN_N3-ON14', name: 'JLPT N3 Online K14 — Tối 2-4-6', courseLabel: 'JLPT N3', branchId: 'cg', teacherIds: ['t-tanaka'], assistantIds: ['ta-mai'], online: true, meetingUrl: 'https://zoom.us/j/111222333', plannedStartDate: d(-40), actualStartDate: d(-40), weekdays: [1, 3, 5], startTime: '18:00', endTime: '20:00', totalHours: 90, minStudents: 6, maxStudents: 18, fee: 6_200_000, status: 'ongoing' },
  // ---- Quận 1
  { id: 'C-N2K05', courseId: 'CRS-04', curriculumId: 'CUR-N2', code: 'GVVN_N2-K05', name: 'JLPT N2 Online K05', courseLabel: 'JLPT N2', branchId: 'q1', teacherIds: ['t-ha', 't-sato'], online: true, meetingUrl: 'https://zoom.us/j/000000000', plannedStartDate: d(-20), actualStartDate: d(-20), weekdays: [2, 4], startTime: '19:30', endTime: '21:30', totalHours: 108, minStudents: 6, maxStudents: 15, fee: 7_500_000, status: 'ongoing' },
  { id: 'C-N5K20', courseId: 'CRS-01', curriculumId: 'CUR-N5-ADV', code: 'GVV_N5-K20', name: 'JLPT N5 K20 (nâng cao) — Tối 2-4-6', courseLabel: 'JLPT N5 cơ bản', branchId: 'q1', teacherIds: ['t-thu'], room: 'P.101', plannedStartDate: d(-15), actualStartDate: d(-15), weekdays: [1, 3, 5], startTime: '18:00', endTime: '20:00', totalHours: 72, minStudents: 8, maxStudents: 20, fee: 4_900_000, status: 'ongoing', note: 'Lớp đầu vào tốt (test TB 86%) → học lộ trình nâng cao.' },
  { id: 'C-KAIWAB1', courseId: 'CRS-05', curriculumId: 'CUR-KAIWA', code: 'GVN_KAIWA-B1', name: 'Kaiwa B1 — Sáng T7', courseLabel: 'Kaiwa giao tiếp', branchId: 'q1', teacherIds: ['t-sato'], assistantIds: ['ta-mai'], room: 'P.203', plannedStartDate: d(-50), actualStartDate: d(-50), weekdays: [6], startTime: '09:00', endTime: '11:00', totalHours: 40, minStudents: 5, maxStudents: 12, fee: 5_200_000, status: 'ongoing' },
  { id: 'C-N3K09', courseId: 'CRS-03', curriculumId: 'CUR-N3-V1', code: 'GVV_N3-K09', name: 'JLPT N3 K09 — Tối 2-4-6', courseLabel: 'JLPT N3', branchId: 'q1', teacherIds: ['t-thu'], room: 'P.102', plannedStartDate: d(-280), actualStartDate: d(-280), actualEndDate: d(-40), weekdays: [1, 3, 5], startTime: '18:00', endTime: '20:00', totalHours: 90, minStudents: 8, maxStudents: 18, fee: 6_800_000, status: 'finished' },
  { id: 'C-N3K13', courseId: 'CRS-03', curriculumId: 'CUR-N3-ADV', code: 'GVV_N3-K13', name: 'JLPT N3 K13 — Tối 3-5', courseLabel: 'JLPT N3', branchId: 'q1', teacherIds: ['t-thu'], room: 'P.102', plannedStartDate: d(-3), weekdays: [2, 4], startTime: '19:00', endTime: '21:00', totalHours: 90, minStudents: 8, maxStudents: 18, fee: 6_800_000, status: 'enrolling', note: 'Đã qua ngày dự kiến, còn thiếu học viên — Sale đang chốt thêm.' },
  // ---- Hải Châu
  { id: 'C-XKLD08', courseId: 'CRS-07', curriculumId: 'CUR-XKLD', code: 'GVV_XKLD-K08', name: 'XKLĐ cấp tốc K08 — Sáng T2–T6', courseLabel: 'Tiếng Nhật đi làm / XKLĐ', branchId: 'hc', teacherIds: ['t-hanh'], assistantIds: ['ta-quan'], room: 'P.A1', plannedStartDate: d(-40), actualStartDate: d(-40), weekdays: [1, 2, 3, 4, 5], startTime: '08:00', endTime: '11:00', totalHours: 180, minStudents: 10, maxStudents: 25, fee: 8_000_000, status: 'ongoing' },
  { id: 'C-N5K07', courseId: 'CRS-01', curriculumId: 'CUR-N5-V1', code: 'GVV_N5-K07', name: 'JLPT N5 K07 — Tối 2-4-6', courseLabel: 'JLPT N5 cơ bản', branchId: 'hc', teacherIds: ['t-hanh'], room: 'P.A2', plannedStartDate: d(-240), actualStartDate: d(-235), actualEndDate: d(-60), weekdays: [1, 3, 5], startTime: '18:00', endTime: '20:00', totalHours: 72, minStudents: 8, maxStudents: 20, fee: 4_900_000, status: 'finished' },
  // Lớp kế tiếp (lên lớp) — đang tuyển, đón học viên từ lớp trước
  { id: 'C-N2K06', courseId: 'CRS-04', curriculumId: 'CUR-N2', code: 'GVVN_N2-K06', name: 'JLPT N2 Online K06 — Tối 2-4-6', courseLabel: 'JLPT N2', branchId: 'cg', teacherIds: ['t-ha', 't-sato'], online: true, meetingUrl: 'https://zoom.us/j/222333444', plannedStartDate: d(30), weekdays: [1, 3, 5], startTime: '18:00', endTime: '20:00', totalHours: 108, minStudents: 6, maxStudents: 15, fee: 7_500_000, status: 'enrolling', previousClassId: 'C-N3K12', note: 'Lớp nối tiếp N3-K12.' },
  { id: 'C-KIDS06', courseId: 'CRS-06', curriculumId: 'CUR-KIDS', code: 'GVV_KIDS-K06', name: 'Thiếu nhi K06 — Sáng cuối tuần (kỳ sau)', courseLabel: 'Tiếng Nhật thiếu nhi', branchId: 'cg', teacherIds: ['t-anh'], room: 'P.205', plannedStartDate: d(24), weekdays: [6, 7], startTime: '09:00', endTime: '11:00', totalHours: 54, minStudents: 6, maxStudents: 15, fee: 4_500_000, status: 'enrolling', previousClassId: 'C-KIDS05', note: 'Kỳ tiếp theo của lớp thiếu nhi K05.' },
  { id: 'C-N4K17', courseId: 'CRS-02', curriculumId: 'CUR-N4-V2', code: 'GVV_N4-K17', name: 'JLPT N4 K17 — Tối 2-4-6', courseLabel: 'JLPT N4', branchId: 'cg', teacherIds: ['t-long'], room: 'P.302', plannedStartDate: d(16), weekdays: [1, 3, 5], startTime: '19:30', endTime: '21:30', totalHours: 96, minStudents: 8, maxStudents: 20, fee: 5_900_000, status: 'enrolling', previousClassId: 'C-N5K18' },
  { id: 'C-N4K16', courseId: 'CRS-02', curriculumId: 'CUR-N4-V2', code: 'GVV_N4-K16', name: 'JLPT N4 K16 — Tối 3-5', courseLabel: 'JLPT N4', branchId: 'hc', teacherIds: ['t-long'], room: 'P.A2', plannedStartDate: d(-10), actualStartDate: d(-10), weekdays: [2, 4], startTime: '18:00', endTime: '20:00', totalHours: 96, minStudents: 8, maxStudents: 20, fee: 5_900_000, status: 'ongoing' },
  { id: 'C-XKLD07', courseId: 'CRS-07', curriculumId: 'CUR-XKLD', code: 'GVV_XKLD-K07', name: 'XKLĐ cấp tốc K07 — Chiều T2–T6', courseLabel: 'Tiếng Nhật đi làm / XKLĐ', branchId: 'hc', teacherIds: ['t-hanh'], room: 'P.A1', plannedStartDate: d(-70), actualStartDate: d(-68), weekdays: [1, 2, 3, 4, 5], startTime: '14:00', endTime: '17:00', totalHours: 180, minStudents: 10, maxStudents: 25, fee: 8_000_000, status: 'paused', note: 'Tạm dừng 2 tuần — đơn hàng dời lịch phỏng vấn, học viên về quê.' },
];

const curriculumOf = (id?: string) => MOCK_CURRICULA.find((c) => c.id === id);

const RECEPTION = ['Lớp nắm bài tốt, hăng hái phát biểu.', 'Phần lớn hiểu bài, 3 bạn còn nhầm cách chia thể.', 'Lớp hơi trầm, cần thêm hoạt động nhóm.', 'Tiếp thu nhanh, kịp làm thêm bài luyện.', 'Phần nghe còn yếu, cho nghe lại 2 lần.'];
const ISSUES = ['2 bạn vắng không phép liên tiếp — nhờ Giáo vụ gọi phụ huynh.', 'Máy chiếu phòng học bị nhòe, đề nghị kiểm tra.', 'Học viên mới vào lớp cần kèm thêm phần chữ cái.'];

/** Nhật ký giáo viên ghi sau buổi (2 buổi gần nhất để trống — GV chưa ghi) */
function logFor(c: Seed, s: ClassSession, i: number, isRecent: boolean): SessionLog | undefined {
  if (isRecent) return undefined;
  const cur = curriculumOf(c.curriculumId);
  const author = teacherName(c.teacherIds[i % c.teacherIds.length]);
  const at = dayjs(`${s.date}T${s.endTime}`).add(15 + (i % 40), 'minute').toISOString();
  if (s.kind === 'exam') {
    const exam = cur?.exams.find((e) => e.id === s.examId);
    return { content: `Tổ chức ${exam?.title.toLowerCase() ?? 'buổi thi'}. Thi nghiêm túc, đúng giờ.`, issues: s.attendance && s.attendance.present < s.attendance.total ? 'Có học viên vắng thi — cần xếp thi bù.' : undefined, author, at };
  }
  const lesson = cur?.lessons.find((l) => l.id === s.lessonId);
  const parts = lesson ? lesson.chapters.slice(i % 2, (i % 2) + 2).map((ch) => ch.title.toLowerCase()).join(', ') : 'nội dung theo giáo trình';
  return {
    content: `${lesson ? `${lesson.title}: ` : ''}dạy phần ${parts}. ${RECEPTION[i % RECEPTION.length]}`,
    homework: i % 3 !== 2 ? `Làm bài tập ${lesson ? `"${lesson.title}"` : ''} trong sách, học thuộc từ mới.` : undefined,
    issues: i % 7 === 4 ? ISSUES[i % ISSUES.length] : undefined,
    author,
    at,
  };
}

/** Sinh buổi học cho lớp + đánh dấu buổi đã qua là "Đã dạy" (kèm điểm danh), 1 buổi hủy, 1 buổi học bù */
function sessionsFor(c: Seed): ClassSession[] {
  if (c.status === 'cancelled' || c.id === 'C-N3K13') return []; // C-N3K13: để trống → demo "Tạo lịch tự động"
  const plan = generateSessions({
    classId: c.id,
    startDate: c.actualStartDate ?? c.plannedStartDate,
    weekdays: c.weekdays as Weekday[],
    startTime: c.startTime,
    endTime: c.endTime,
    count: totalSessionsOf(c),
    skipHolidays: true,
    curriculum: curriculumOf(c.curriculumId),
  });
  const today = dayjs().format('YYYY-MM-DD');
  const size = Math.min(c.maxStudents - 4, 14);
  const pausedFrom = c.status === 'paused' ? d(-14) : undefined;
  const pastCount = plan.sessions.filter((s) => s.date < today && (!pausedFrom || s.date < pausedFrom)).length;
  return plan.sessions.map((s, i) => {
    const past = s.date < today && (!pausedFrom || s.date < pausedFrom);
    const recent = c.status === 'ongoing' && i >= pastCount - 2;
    if (!past) return { ...s, id: `${c.id}-S${s.no}`, note: pausedFrom && s.date >= pausedFrom && s.date < today ? 'Lớp tạm dừng' : undefined, status: pausedFrom && s.date >= pausedFrom && s.date < today ? 'cancelled' : 'scheduled' };
    if (i === 7) return { ...s, id: `${c.id}-S${s.no}`, status: 'cancelled', note: 'GV ốm — đã xếp học bù' };
    if (i === 9) {
      const done: ClassSession = { ...s, id: `${c.id}-S${s.no}`, status: 'makeup', note: 'Học bù cho buổi 8', attendance: { present: size - 2, total: size } };
      return { ...done, teacherLog: logFor(c, done, i, recent) };
    }
    const done: ClassSession = { ...s, id: `${c.id}-S${s.no}`, status: 'completed', attendance: { present: size - (i % 4 === 0 ? 2 : i % 5 === 0 ? 1 : 0), total: size } };
    return { ...done, teacherLog: logFor(c, done, i, recent) };
  });
}

export const MOCK_SESSIONS: ClassSession[] = seeds.flatMap(sessionsFor);

export const MOCK_CLASSES: ClassInfo[] = seeds.map((c, i) => {
  const mine = MOCK_SESSIONS.filter((s) => s.classId === c.id);
  return {
    ...c,
    plannedEndDate: c.plannedEndDate ?? mine[mine.length - 1]?.date ?? dayjs(c.plannedStartDate).add(Math.ceil(totalSessionsOf(c) / c.weekdays.length), 'week').format('YYYY-MM-DD'),
    createdAt: ago(Math.max(10, dayjs().diff(dayjs(c.plannedStartDate), 'day') + 30 + i)),
  };
});

/** Bài tập: mỗi bài học đã dạy xong có 1 bài tập */
export const MOCK_ASSIGNMENTS: Assignment[] = MOCK_CLASSES.flatMap((c) => {
  const cur = curriculumOf(c.curriculumId);
  if (!cur) return [];
  const done = MOCK_SESSIONS.filter((s) => s.classId === c.id && s.kind === 'lesson' && (s.status === 'completed' || s.status === 'makeup'));
  const lessonIds = [...new Set(done.map((s) => s.lessonId))].filter(Boolean).slice(-6) as string[];
  return lessonIds.map((lid, i) => {
    const lesson = cur.lessons.find((l) => l.id === lid)!;
    const last = done.filter((s) => s.lessonId === lid).pop()!;
    const due = dayjs(last.date).add(4, 'day');
    return {
      id: `${c.id}-A${i + 1}`,
      classId: c.id,
      title: i % 2 === 0 ? `Trắc nghiệm: ${lesson.title}` : `Bài tập: ${lesson.title}`,
      lessonId: lid,
      // Xen kẽ: bài trắc nghiệm từ Ngân hàng câu hỏi / bài nộp ảnh-file
      type: i % 2 === 0 ? ('quiz' as const) : ('upload' as const),
      description:
        i % 2 === 0 ? 'Làm trắc nghiệm trên Cổng học viên — đề bốc từ Ngân hàng câu hỏi.' : 'Làm bài tập trong sách + ghi âm phần luyện nói, chụp ảnh nộp trên Cổng học viên.',
      quiz:
        i % 2 === 0
          ? { lessonIds: [lid], skills: ['vocab', 'kanji', 'grammar'] as ('vocab' | 'kanji' | 'grammar')[], difficulty: 'mixed' as const, questionCount: 20, timeLimitMinutes: 25, attempts: 2, shuffle: true }
          : undefined,
      assignedAt: dayjs(last.date).hour(21).toISOString(),
      dueDate: due.format('YYYY-MM-DD'),
      maxScore: 10,
    };
  });
});
