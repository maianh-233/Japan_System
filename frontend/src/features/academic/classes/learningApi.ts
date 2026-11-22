/**
 * Nội dung học (user story 12, giai đoạn 20) — backend Academic: bài tập, kỳ thi, thông báo lớp, đánh giá GV, flashcard; Library: tài liệu GV.
 * Đọc rồi ĐỔ VÀO store cũ (useClassStore.assignments / submissions / examResults / announcements / teacherReviews / classExams,
 * usePortalStore.decks / cardProgress / teacherDocs / myExams / inbox) để các trang giữ nguyên; thao tác ghi gọi server rồi cập nhật store.
 * Nhân viên tải theo lớp khi mở trang lớp; GV tải các lớp mình dạy khi vào cổng; HV / PH tải của mình / của con.
 * Quiz / bài thi: câu hỏi KHÔNG kèm đáp án, server chấm (quy tắc vàng 2).
 */
import dayjs from 'dayjs';
import { api } from '@/lib/api/http';
import { fromGuid, fromGuids, toGuid, toGuids } from '@/lib/api/ids';
import { useCrmStore } from '@/features/crm/crmStore';
import { toCurriculum, type ServerCurriculum } from '@/features/academic/curriculum/curriculumApi';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { usePortalStore } from '@/features/portal/portalStore';
import type { CardProgress, FlashDeck, MyExam, PortalAnnouncement, TeacherDoc } from '@/features/portal/types';
import { useClassStore } from './classStore';
import type {
  Assignment,
  AssignmentType,
  ClassAnnouncement,
  ClassExam,
  ExamMode,
  ExamResult,
  QuizConfig,
  QuizSkill,
  Submission,
  SubmissionFile,
  TeacherReview,
} from './types';

// ---------------- Kiểu dữ liệu server ----------------

interface ServerFile {
  name: string;
  kind: SubmissionFile['kind'] | 'video';
  url: string;
  size?: number | null;
}

interface ServerAssignment {
  id: string;
  classId: string;
  type: AssignmentType;
  title: string;
  lessonId?: string | null;
  description?: string | null;
  assignedAt: string;
  dueDate: string;
  maxScore: number;
  quiz?: (Omit<QuizConfig, 'timeLimitMinutes'> & { timeLimitMinutes?: number | null }) | null;
  attachments: ServerFile[];
}

interface ServerSubmission {
  id: string;
  assignmentId: string;
  studentId: string;
  submittedAt: string;
  files: ServerFile[];
  quiz?: { correct: number; total: number; attempts: number; durationMinutes: number } | null;
  content?: string | null;
  score?: number | null;
  comment?: string | null;
  gradedAt?: string | null;
  gradedBy?: string | null;
}

interface ServerExamSection {
  name: string;
  skill?: QuizSkill | null;
  points: number;
  questionCount: number;
  max: number;
  questionIds: string[];
}

interface ServerClassExam extends Omit<ClassExam, 'sections' | 'kind' | 'publishedAt' | 'resultsPublishedAt'> {
  kind?: ClassExam['kind'] | null;
  sections: ServerExamSection[];
  publishedAt?: string | null;
  resultsPublishedAt?: string | null;
}

interface ServerExamResult {
  id: string;
  classId: string;
  examId: string;
  classExamId?: string | null;
  studentId: string;
  status: ExamResult['status'];
  sections: { name: string; score: number; max: number }[];
  startedAt?: string | null;
  durationMinutes?: number | null;
  comment?: string | null;
}

export interface ExamMilestone {
  id: string;
  kind: 'midterm' | 'final';
  title: string;
  afterLessonId: string;
  lessonIds: string[];
  classExamId?: string | null;
}

interface ServerAnnouncement {
  id: string;
  classId: string;
  title: string;
  content: string;
  audience: ClassAnnouncement['audience'];
  channels: ClassAnnouncement['channels'];
  debtorsOnly: boolean;
  studentIds: string[];
  recipients: number;
  readCount: number;
  sentBy: string;
  sentAt?: string | null;
  scheduledAt?: string | null;
  cancelledAt?: string | null;
  status: 'scheduled' | 'sent' | 'cancelled';
}

interface ServerReview {
  id: string;
  classId: string;
  teacherId: string;
  studentId?: string | null;
  period: TeacherReview['period'];
  overall: number;
  ratings: TeacherReview['ratings'];
  comment?: string | null;
  anonymous: boolean;
  createdAt: string;
}

/** 1 câu trong đề phát cho HV — không đáp án; options theo thứ tự hiển thị */
export interface PaperQuestion {
  id: string;
  section: number;
  skill: string;
  format: string;
  stem: string;
  passage?: string | null;
  audioUrl?: string | null;
  audioDurationSec?: number | null;
  options: string[];
}

/** Xem lại 1 câu sau khi nộp — chosen / answer theo vị trí hiển thị */
export interface ReviewQuestion {
  questionId: string;
  section: number;
  stem: string;
  passage?: string | null;
  options: string[];
  chosen?: number | null;
  answer: number;
  correct: boolean;
  explanation?: string | null;
}

interface ServerDeck {
  id: string;
  title: string;
  level: string;
  kind: FlashDeck['kind'];
  lessonId?: string | null;
  lesson: string;
  cards: { id: string; front: string; reading?: string | null; meaning: string; example?: string | null; order: number }[];
}

interface ServerDoc {
  id: string;
  teacherId: string;
  title: string;
  description?: string | null;
  fileName: string;
  kind: TeacherDoc['kind'];
  size?: number | null;
  url?: string | null;
  classIds: string[];
  sessionId?: string | null;
  createdAt: string;
  viewedBy: string[];
  viewed: boolean;
}

// ---------------- Đổi dữ liệu ----------------

const u = <T>(v: T | null | undefined) => (v === null ? undefined : v);
const fileKind = (k: ServerFile['kind']): SubmissionFile['kind'] => (k === 'video' ? 'doc' : k);
const toFile = (f: ServerFile): SubmissionFile => ({ name: f.name, kind: fileKind(f.kind), url: f.url, size: u(f.size) });
/** Gửi lên server: giữ nguyên khóa / link server cấp */
const fromFile = (f: SubmissionFile) => ({ name: f.name, kind: f.kind, url: f.url, size: f.size ?? null });

export const toAssignment = (a: ServerAssignment): Assignment => ({
  id: fromGuid(a.id),
  classId: fromGuid(a.classId),
  type: a.type,
  title: a.title,
  lessonId: u(a.lessonId),
  description: u(a.description),
  assignedAt: a.assignedAt,
  dueDate: a.dueDate,
  maxScore: a.maxScore,
  quiz: a.quiz ? { ...a.quiz, timeLimitMinutes: u(a.quiz.timeLimitMinutes) } : undefined,
  attachments: a.attachments.length ? a.attachments.map(toFile) : undefined,
});

export const toSubmission = (s: ServerSubmission): Submission => ({
  id: fromGuid(s.id),
  assignmentId: fromGuid(s.assignmentId),
  studentId: fromGuid(s.studentId),
  submittedAt: s.submittedAt,
  files: s.files.map(toFile),
  quiz: u(s.quiz),
  content: u(s.content),
  score: u(s.score),
  comment: u(s.comment),
  gradedAt: u(s.gradedAt),
  gradedBy: s.gradedBy ?? (s.quiz ? 'Tự chấm' : undefined),
});

const toExam = (e: ServerClassExam): ClassExam => ({
  ...e,
  id: fromGuid(e.id),
  classId: fromGuid(e.classId),
  kind: u(e.kind),
  publishedAt: u(e.publishedAt),
  resultsPublishedAt: u(e.resultsPublishedAt),
  sections: e.sections.map((s) => ({ ...s, skill: u(s.skill) })),
});

const toResult = (r: ServerExamResult): ExamResult => ({
  id: fromGuid(r.id),
  classId: fromGuid(r.classId),
  examId: r.examId,
  classExamId: u(r.classExamId),
  studentId: fromGuid(r.studentId),
  status: r.status,
  sections: r.sections,
  startedAt: u(r.startedAt),
  durationMinutes: u(r.durationMinutes),
  comment: u(r.comment),
});

const toAnnouncement = (a: ServerAnnouncement): ClassAnnouncement => ({
  id: fromGuid(a.id),
  classId: fromGuid(a.classId),
  title: a.title,
  content: a.content,
  audience: a.audience,
  channels: a.channels,
  debtorsOnly: a.debtorsOnly || undefined,
  studentIds: a.studentIds.length ? fromGuids(a.studentIds) : undefined,
  recipients: a.recipients,
  readCount: a.readCount,
  sentBy: a.sentBy,
  sentAt: a.sentAt ?? a.scheduledAt ?? a.cancelledAt ?? '',
  scheduledAt: a.status === 'sent' ? undefined : u(a.scheduledAt),
  status: a.status,
  cancelledAt: u(a.cancelledAt),
});

const toReview = (r: ServerReview): TeacherReview => ({
  id: fromGuid(r.id),
  classId: fromGuid(r.classId),
  teacherId: fromGuid(r.teacherId),
  studentId: r.studentId ? fromGuid(r.studentId) : '',
  period: r.period,
  overall: r.overall,
  ratings: r.ratings,
  comment: u(r.comment),
  anonymous: r.anonymous,
  createdAt: r.createdAt,
});

const toDeck = (d: ServerDeck): FlashDeck => ({
  id: fromGuid(d.id),
  title: d.title,
  level: d.level as FlashDeck['level'],
  kind: d.kind,
  lesson: d.lesson,
  cards: [...d.cards].sort((a, b) => a.order - b.order).map((c) => ({ id: fromGuid(c.id), front: c.front, reading: u(c.reading), meaning: c.meaning, example: u(c.example) })),
});

const toDoc = (d: ServerDoc, viewer?: string): TeacherDoc => ({
  id: fromGuid(d.id),
  teacherId: fromGuid(d.teacherId),
  title: d.title,
  description: u(d.description),
  fileName: d.fileName,
  kind: d.kind,
  size: u(d.size),
  url: u(d.url),
  classIds: fromGuids(d.classIds),
  sessionId: d.sessionId ? fromGuid(d.sessionId) : undefined,
  createdAt: d.createdAt,
  viewedBy: d.viewedBy.length ? fromGuids(d.viewedBy) : d.viewed && viewer ? [viewer] : [],
});

/** Thay các phần tử thuộc lớp / khóa bằng danh sách mới */
const replaceBy = <T>(list: T[], next: T[], drop: (x: T) => boolean) => [...next, ...list.filter((x) => !drop(x))];
const upsert = <T extends { id: string }>(list: T[], item: T) => [item, ...list.filter((x) => x.id !== item.id)];

/** Hạn nộp chọn theo ngày → cuối ngày giờ VN */
const endOfDay = (d: string) => dayjs(d).endOf('day').toISOString();

const base = (asTeacher: boolean) => (asTeacher ? '/academic/portal/teaching' : '/academic');

// ---------------- Nhân viên / GV: theo lớp ----------------

const milestonesByClass = new Map<string, ExamMilestone[]>();
/** Mốc thi + phạm vi bài của lớp (từ lần tải gần nhất) */
export const milestonesOf = (classId: string) => milestonesByClass.get(classId) ?? [];

/** Tab Bài tập / Bài thi / Thông báo / Đánh giá GV của 1 lớp. GV: không tải đánh giá (xem ở /reviews). */
export async function loadClassLearning(classId: string, asTeacher = false) {
  const id = toGuid(classId);
  const b = base(asTeacher);
  const [assign, exams, anns, reviews] = await Promise.all([
    api<{ assignments: ServerAssignment[]; submissions: ServerSubmission[] }>(`${b}/classes/${id}/assignments`),
    api<{ milestones: ExamMilestone[]; exams: ServerClassExam[]; results: ServerExamResult[] }>(`${b}/classes/${id}/exams`),
    api<ServerAnnouncement[]>(`${b}/classes/${id}/announcements`),
    asTeacher ? Promise.resolve(null) : api<ServerReview[]>(`/academic/classes/${id}/teacher-reviews`),
  ]);
  milestonesByClass.set(classId, exams.milestones);
  const assignments = assign.assignments.map(toAssignment);
  const assignmentIds = new Set(assignments.map((a) => a.id));
  useClassStore.setState((s) => ({
    assignments: replaceBy(s.assignments, assignments, (a) => a.classId === classId),
    submissions: [
      ...assign.submissions.map(toSubmission),
      ...s.submissions.filter((x) => !assignmentIds.has(x.assignmentId) && !s.assignments.some((a) => a.id === x.assignmentId && a.classId === classId)),
    ],
    classExams: replaceBy(s.classExams, exams.exams.map(toExam), (e) => e.classId === classId),
    examResults: replaceBy(s.examResults, exams.results.map(toResult), (r) => r.classId === classId),
    announcements: replaceBy(s.announcements, anns.map(toAnnouncement), (a) => a.classId === classId),
    teacherReviews: reviews ? replaceBy(s.teacherReviews, reviews.map(toReview), (r) => r.classId === classId) : s.teacherReviews,
  }));
}

/** Cổng GV: lộ trình các lớp mình dạy (bài học, mốc thi) — GV không có quyền trang Lộ trình */
async function loadTeachingCurricula(classIds: string[]) {
  const list = await Promise.all(
    classIds.map((id) => api<ServerCurriculum>(`/academic/portal/teaching/classes/${toGuid(id)}/curriculum`).catch(() => undefined)),
  );
  const seen = new Map(list.filter((c): c is ServerCurriculum => Boolean(c)).map((c) => [c.id, toCurriculum(c)]));
  useCurriculumStore.setState({ curricula: [...seen.values()] });
}

/** Cổng GV: nội dung học các lớp mình dạy + đánh giá về mình + tài liệu của mình */
export async function loadTeacherLearning() {
  // Bỏ lớp tạm dựng từ lời mời dạy thay (không có khóa học — GV chưa dạy lớp đó)
  const classes = useClassStore.getState().classes.filter((c) => c.courseId);
  const quiet = (p: Promise<unknown>) => p.catch(() => undefined);
  await Promise.all([
    quiet(loadTeachingCurricula(classes.filter((c) => c.curriculumId).map((c) => c.id))),
    ...classes.map((c) => quiet(loadClassLearning(c.id, true))),
    quiet(api<ServerReview[]>('/academic/portal/teaching/reviews').then((list) => useClassStore.setState({ teacherReviews: list.map(toReview) }))),
    quiet(loadTeacherDocs()),
  ]);
}

// ---------- Bài tập (ND-01, 03) ----------

export interface AssignmentInput {
  type: AssignmentType;
  title: string;
  lessonId?: string;
  description?: string;
  /** "YYYY-MM-DD" hoặc ISO */
  dueDate: string;
  maxScore?: number;
  quiz?: QuizConfig;
  attachments?: SubmissionFile[];
}

const assignmentBody = (v: AssignmentInput) => ({
  type: v.type,
  title: v.title,
  lessonId: v.lessonId ?? null,
  description: v.description ?? null,
  dueDate: v.dueDate.length === 10 ? endOfDay(v.dueDate) : v.dueDate,
  maxScore: v.maxScore ?? null,
  quiz: v.type === 'quiz' && v.quiz ? { ...v.quiz, timeLimitMinutes: v.quiz.timeLimitMinutes ?? null } : null,
  attachments: (v.attachments ?? []).map(fromFile),
});

const putAssignment = (a: Assignment) => useClassStore.setState((s) => ({ assignments: upsert(s.assignments, a) }));

/** Giao bài — trả cảnh báo "Chỉ có N câu phù hợp" (ND-01.E4) nếu có */
export async function createAssignment(classId: string, v: AssignmentInput, asTeacher = false): Promise<string | undefined> {
  const r = await api<{ assignment: ServerAssignment; warning?: string | null }>(`${base(asTeacher)}/classes/${toGuid(classId)}/assignments`, {
    body: assignmentBody(v),
  });
  putAssignment(toAssignment(r.assignment));
  return u(r.warning);
}

export async function updateAssignment(a: Assignment, patch: Partial<AssignmentInput>, asTeacher = false): Promise<string | undefined> {
  const r = await api<{ assignment: ServerAssignment; warning?: string | null }>(`${base(asTeacher)}/assignments/${toGuid(a.id)}`, {
    method: 'PUT',
    body: assignmentBody({ ...a, ...patch } as AssignmentInput),
  });
  putAssignment(toAssignment(r.assignment));
  return u(r.warning);
}

export async function deleteAssignment(id: string, asTeacher = false) {
  await api(`${base(asTeacher)}/assignments/${toGuid(id)}`, { method: 'DELETE' });
  useClassStore.setState((s) => ({ assignments: s.assignments.filter((x) => x.id !== id), submissions: s.submissions.filter((x) => x.assignmentId !== id) }));
}

export async function gradeSubmission(id: string, score: number, comment: string | undefined, asTeacher = false) {
  const sub = toSubmission(
    await api<ServerSubmission>(`${base(asTeacher)}/submissions/${toGuid(id)}/grade`, { method: 'PUT', body: { score, comment: comment ?? null } }),
  );
  useClassStore.setState((s) => ({ submissions: s.submissions.map((x) => (x.id === sub.id ? sub : x)) }));
}

/** Tải 1 tệp (đề GV / bài làm HV) → tệp đính kèm có khóa kho server */
export async function uploadLearningFile(file: File, portal: 'staff' | 'teacher' | 'student'): Promise<SubmissionFile> {
  const form = new FormData();
  form.append('file', file);
  const path = portal === 'staff' ? '/academic/assignments/files' : portal === 'teacher' ? '/academic/portal/teaching/files' : '/academic/portal/learning/files';
  return toFile(await api<ServerFile>(path, { body: form }));
}

/** Link tạm mở đề đính kèm / tệp bài nộp */
export async function fileLink(
  viewer: 'staff' | 'teacher' | 'student',
  assignmentId: string,
  index: number,
  submissionId?: string,
  studentId?: string,
): Promise<string> {
  const prefix = viewer === 'staff' ? '/academic' : viewer === 'teacher' ? '/academic/portal/teaching' : '/academic/portal/learning';
  const tail = submissionId ? `submissions/${toGuid(submissionId)}/files/${index}/link` : `attachments/${index}/link`;
  const q = studentId ? `?studentId=${toGuid(studentId)}` : '';
  return (await api<{ url: string }>(`${prefix}/assignments/${toGuid(assignmentId)}/${tail}${q}`)).url;
}

// ---------- Kỳ thi (ND-04, 06) ----------

export interface ExamSectionInput {
  name: string;
  skill?: QuizSkill;
  points: number;
  count?: number;
  questionIds?: string[];
}

export interface ExamInput {
  examId: string;
  title: string;
  mode: ExamMode;
  startsAt: string;
  durationMinutes: number;
  passPercent?: number;
  shuffle: boolean;
  sections: ExamSectionInput[];
}

export interface ExamPaperQuestion {
  id: string;
  code: string;
  skill: string;
  difficulty: string;
  stem: string;
  passage?: string | null;
  options: string[];
  answer: number;
  explanation?: string | null;
}

export interface ExamPaperSection {
  name: string;
  skill?: QuizSkill | null;
  points: number;
  questions: ExamPaperQuestion[];
}

const examBody = (v: ExamInput) => ({
  ...v,
  passPercent: v.passPercent ?? null,
  sections: v.sections.map((s) => ({ name: s.name, skill: s.skill ?? null, points: s.points, count: s.count ?? null, questionIds: s.questionIds ?? null })),
});

const putExam = (e: ServerClassExam) => {
  const exam = toExam(e);
  useClassStore.setState((s) => ({ classExams: upsert(s.classExams, exam) }));
  return exam;
};

/** Xem trước đề (bốc câu đã duyệt trong phạm vi bài của mốc) — chưa lưu */
export const drawExam = (classId: string, examId: string, sections: ExamSectionInput[], exclude?: string[]) =>
  api<ExamPaperSection[]>(`/academic/classes/${toGuid(classId)}/exams/draw`, {
    body: { examId, sections: examBody({ sections } as ExamInput).sections, exclude: exclude ?? null },
  });

export const createExam = async (classId: string, v: ExamInput) =>
  putExam(await api<ServerClassExam>(`/academic/classes/${toGuid(classId)}/exams`, { body: examBody(v) }));

export const updateExam = async (id: string, v: ExamInput) => putExam(await api<ServerClassExam>(`/academic/exams/${id}`, { method: 'PUT', body: examBody(v) }));

export const changeExam = async (id: string, action: 'publish' | 'cancel' | 'publish-results', asTeacher = false) =>
  putExam(await api<ServerClassExam>(`${base(asTeacher)}/exams/${id}/${action}`, { method: 'POST' }));

export const examPaper = (id: string, asTeacher = false) =>
  api<{ exam: ServerClassExam; sections: ExamPaperSection[] }>(`${base(asTeacher)}/exams/${id}/paper`);

export interface ExamResultRow {
  studentId: string;
  code: string;
  fullName: string;
  attempt: 'none' | 'in_progress' | 'submitted';
  result?: ExamResult;
}

/** Trang Kết quả kỳ thi: danh sách lớp + trạng thái làm bài + kết quả */
export async function loadExamResults(id: string, asTeacher = false): Promise<{ exam: ClassExam; rows: ExamResultRow[] }> {
  const r = await api<{ exam: ServerClassExam; rows: (Omit<ExamResultRow, 'result'> & { result?: ServerExamResult | null })[] }>(
    `${base(asTeacher)}/exams/${id}/results`,
  );
  const exam = putExam(r.exam);
  const rows = r.rows.map((x) => ({ ...x, studentId: fromGuid(x.studentId), result: x.result ? toResult(x.result) : undefined }));
  const results = rows.flatMap((x) => (x.result ? [x.result] : []));
  useClassStore.setState((s) => ({ examResults: replaceBy(s.examResults, results, (x) => x.classId === exam.classId && x.examId === exam.examId) }));
  return { exam, rows };
}

/** Nhập / sửa điểm từng phần (theo thứ tự phần của đề) hoặc vắng thi */
export async function saveExamResult(id: string, studentId: string, v: { absent: boolean; scores?: number[]; comment?: string }, asTeacher = false) {
  const res = toResult(
    await api<ServerExamResult>(`${base(asTeacher)}/exams/${id}/results/${toGuid(studentId)}`, {
      method: 'PUT',
      body: { absent: v.absent, scores: v.scores ?? null, comment: v.comment ?? null },
    }),
  );
  useClassStore.setState((s) => ({ examResults: upsert(s.examResults, res) }));
  return res;
}

// ---------- Thông báo lớp (ND-07) ----------

export interface AnnouncementInput {
  title: string;
  content: string;
  audience: ClassAnnouncement['audience'];
  channels: ClassAnnouncement['channels'];
  debtorsOnly?: boolean;
  studentIds?: string[];
  scheduledAt?: string;
}

export async function sendAnnouncement(classId: string, v: AnnouncementInput, asTeacher = false) {
  const a = toAnnouncement(
    await api<ServerAnnouncement>(`${base(asTeacher)}/classes/${toGuid(classId)}/announcements`, {
      body: { ...v, debtorsOnly: v.debtorsOnly ?? false, studentIds: v.studentIds ? toGuids(v.studentIds) : null, scheduledAt: v.scheduledAt ?? null },
    }),
  );
  useClassStore.setState((s) => ({ announcements: upsert(s.announcements, a) }));
  return a;
}

export async function cancelAnnouncement(id: string, asTeacher = false) {
  const a = toAnnouncement(await api<ServerAnnouncement>(`${base(asTeacher)}/announcements/${toGuid(id)}`, { method: 'DELETE' }));
  useClassStore.setState((s) => ({ announcements: upsert(s.announcements, a) }));
}

// ---------- Flashcard — soạn bộ thẻ (ND-09, academic.curriculum) ----------

export interface DeckInput {
  title: string;
  level: string;
  kind: FlashDeck['kind'];
  lessonId?: string;
  lessonLabel?: string;
  cards?: { front: string; reading?: string; meaning: string; example?: string }[];
}

export const loadDecks = async (curriculumId?: string) =>
  (await api<ServerDeck[]>(`/academic/flash-decks${curriculumId ? `?curriculumId=${toGuid(curriculumId)}` : ''}`)).map((d) => ({
    ...toDeck(d),
    lessonId: u(d.lessonId),
  }));

const deckBody = (v: DeckInput) => ({ ...v, level: v.level.toLowerCase(), lessonId: v.lessonId ?? null, lessonLabel: v.lessonLabel ?? null });
export const saveDeck = async (v: DeckInput, id?: string) =>
  toDeck(await api<ServerDeck>(id ? `/academic/flash-decks/${toGuid(id)}` : '/academic/flash-decks', { method: id ? 'PUT' : 'POST', body: deckBody(v) }));
export const deleteDeck = (id: string) => api(`/academic/flash-decks/${toGuid(id)}`, { method: 'DELETE' });
export const addCards = async (id: string, cards: NonNullable<DeckInput['cards']>) =>
  toDeck(await api<ServerDeck>(`/academic/flash-decks/${toGuid(id)}/cards`, { body: { cards } }));
export const removeCard = async (id: string, cardId: string) =>
  toDeck(await api<ServerDeck>(`/academic/flash-decks/${toGuid(id)}/cards/${toGuid(cardId)}`, { method: 'DELETE' }));

// ---------------- Tài liệu GV (ND-10, module Library) ----------------

export async function loadTeacherDocs() {
  usePortalStore.setState({ teacherDocs: (await api<ServerDoc[]>('/library/portal/teaching/documents')).map((d) => toDoc(d)) });
}

export interface DocInput {
  title: string;
  description?: string;
  /** File mới (tải lên trước) hoặc link; bỏ trống khi sửa = giữ nội dung cũ */
  file?: File;
  link?: string;
  classIds: string[];
  sessionId?: string;
}

async function docContent(v: DocInput, current?: TeacherDoc) {
  if (v.file) {
    const form = new FormData();
    form.append('file', v.file);
    return api<{ name: string; kind: TeacherDoc['kind']; url: string; size?: number }>('/library/portal/teaching/documents/files', { body: form });
  }
  if (v.link) return { name: v.link, kind: 'link' as const, url: v.link, size: null };
  if (!current) throw new Error('Chọn file hoặc dán link');
  // Giữ nội dung cũ: server so khóa / link hiện tại (file: url không trả về cổng → gửi lại theo tên để server nhận "không đổi")
  return current.kind === 'link' ? { name: current.fileName, kind: 'link' as const, url: current.url!, size: null } : null;
}

export async function saveTeacherDoc(v: DocInput, current?: TeacherDoc) {
  const file = await docContent(v, current);
  const body = { title: v.title, description: v.description ?? null, file, classIds: toGuids(v.classIds), sessionId: v.sessionId ? toGuid(v.sessionId) : null };
  const d = toDoc(
    await api<ServerDoc>(current ? `/library/portal/teaching/documents/${toGuid(current.id)}` : '/library/portal/teaching/documents', {
      method: current ? 'PUT' : 'POST',
      body,
    }),
  );
  usePortalStore.setState((s) => ({ teacherDocs: upsert(s.teacherDocs, d) }));
}

export async function deleteTeacherDoc(id: string) {
  await api(`/library/portal/teaching/documents/${toGuid(id)}`, { method: 'DELETE' });
  usePortalStore.setState((s) => ({ teacherDocs: s.teacherDocs.filter((x) => x.id !== id) }));
}

/** Mở tài liệu: GV (của mình) hoặc HV / PH (ghi lượt xem khi HV mở) */
export async function openTeacherDoc(id: string, asTeacher: boolean, studentId?: string) {
  const q = studentId ? `?studentId=${toGuid(studentId)}` : '';
  const path = asTeacher ? `/library/portal/teaching/documents/${toGuid(id)}/link` : `/library/portal/learning/documents/${toGuid(id)}/link${q}`;
  const { url } = await api<{ url: string }>(path);
  if (!asTeacher && studentId)
    usePortalStore.setState((s) => ({
      teacherDocs: s.teacherDocs.map((d) => (d.id === id && !d.viewedBy.includes(studentId) ? { ...d, viewedBy: [...d.viewedBy, studentId] } : d)),
    }));
  return url;
}

// ---------------- Cổng HV / PH ----------------

interface ServerMyAssignment {
  assignment: ServerAssignment;
  classCode: string;
  submission?: ServerSubmission | null;
  attemptsLeft?: number | null;
  canSubmit: boolean;
}

interface ServerMyExam {
  exam: ServerClassExam;
  classCode: string;
  attempt: MyExam['attempt'];
  attemptId?: string | null;
  canStart: boolean;
  result?: ServerExamResult | null;
  passed?: boolean | null;
}

/** HV của tài khoản: HV → chính mình; PH → các con (đã nạp ở loadLearningPortal) */
const learnersOf = () => useCrmStore.getState().students.map((s) => s.id);

/** Cổng HV / PH: bài tập, kỳ thi, thông báo lớp, tài liệu GV, flashcard của HV (PH: từng con) */
export async function loadLearnerLearning(isParent: boolean) {
  const quiet = <T>(p: Promise<T>) => p.catch(() => undefined);
  const ids = learnersOf();
  const per = await Promise.all(
    ids.map(async (sid) => {
      const q = `?studentId=${toGuid(sid)}`;
      const [assigns, exams, anns, docs, cards] = await Promise.all([
        quiet(api<ServerMyAssignment[]>(`/academic/portal/learning/assignments${q}`)),
        quiet(api<ServerMyExam[]>(`/academic/portal/learning/exams${q}`)),
        quiet(api<ServerAnnouncementInbox[]>(`/academic/portal/learning/announcements${q}`)),
        quiet(api<ServerDoc[]>(`/library/portal/learning/documents${q}`)),
        isParent ? Promise.resolve(undefined) : quiet(api<ServerFlashcards>(`/academic/portal/learning/flashcards${q}`)),
      ]);
      return { sid, assigns: assigns ?? [], exams: exams ?? [], anns: anns ?? [], docs: docs ?? [], cards };
    }),
  );

  const assignments = new Map<string, Assignment>();
  const submissions: Submission[] = [];
  const results: ExamResult[] = [];
  const classExams = new Map<string, ClassExam>();
  const myExams: MyExam[] = [];
  const inbox = new Map<string, PortalAnnouncement>();
  const docs = new Map<string, TeacherDoc>();
  per.forEach((p) => {
    p.assigns.forEach((x) => {
      const a = toAssignment(x.assignment);
      assignments.set(a.id, a);
      if (x.submission) submissions.push(toSubmission(x.submission));
    });
    p.exams.forEach((x) => {
      const exam = toExam(x.exam);
      classExams.set(exam.id, exam);
      const result = x.result ? toResult(x.result) : undefined;
      if (result) results.push(result);
      myExams.push({ exam, studentId: p.sid, classCode: x.classCode, attempt: x.attempt, attemptId: u(x.attemptId), canStart: x.canStart, result, passed: u(x.passed) });
    });
    p.anns.forEach((x) => inbox.set(x.id, { ...x, id: fromGuid(x.id), classId: fromGuid(x.classId) }));
    p.docs.forEach((x) => {
      const d = toDoc(x, p.sid);
      const prev = docs.get(d.id);
      docs.set(d.id, prev ? { ...d, viewedBy: [...new Set([...prev.viewedBy, ...d.viewedBy])] } : d);
    });
    if (p.cards) applyFlashcards(p.cards);
  });
  useClassStore.setState({ assignments: [...assignments.values()], submissions, examResults: results, classExams: [...classExams.values()] });
  usePortalStore.setState({ myExams, inbox: [...inbox.values()].sort((a, b) => b.sentAt.localeCompare(a.sentAt)), teacherDocs: [...docs.values()] });
}

interface ServerAnnouncementInbox extends Omit<PortalAnnouncement, 'classId'> {
  classId: string;
}

export async function markAnnouncementRead(id: string) {
  await api(`/academic/portal/learning/announcements/${toGuid(id)}/read`, { method: 'POST' });
  usePortalStore.setState((s) => ({ inbox: s.inbox.map((x) => (x.id === id ? { ...x, read: true } : x)) }));
}

// ---------- Bài tập ở cổng HV (ND-02) ----------

const putSubmission = (sub: Submission) =>
  useClassStore.setState((s) => ({ submissions: [sub, ...s.submissions.filter((x) => x.id !== sub.id && !(x.assignmentId === sub.assignmentId && x.studentId === sub.studentId))] }));

export async function submitAssignment(assignmentId: string, files: File[], content?: string) {
  const uploaded = await Promise.all(files.map((f) => uploadLearningFile(f, 'student')));
  putSubmission(
    toSubmission(
      await api<ServerSubmission>(`/academic/portal/learning/assignments/${toGuid(assignmentId)}/submissions`, {
        body: { files: uploaded.map(fromFile), content: content ?? null },
      }),
    ),
  );
}

export interface QuizPaper {
  token: string;
  assignmentId: string;
  startedAt: string;
  deadlineAt?: string | null;
  attemptsLeft: number;
  questions: PaperQuestion[];
}

export const startQuiz = (assignmentId: string) => api<QuizPaper>(`/academic/portal/learning/assignments/${toGuid(assignmentId)}/quiz/start`, { method: 'POST' });

export interface QuizOutcome {
  correct: number;
  total: number;
  score: number;
  maxScore: number;
  timedOut: boolean;
  review: ReviewQuestion[];
}

/** Nộp quiz: answers = câu → vị trí đáp án hiển thị */
export async function submitQuiz(assignmentId: string, token: string, answers: Record<string, number>): Promise<QuizOutcome> {
  const r = await api<QuizOutcome & { submission: ServerSubmission }>(`/academic/portal/learning/assignments/${toGuid(assignmentId)}/quiz/submit`, {
    body: { token, answers: Object.entries(answers).map(([questionId, choice]) => ({ questionId, choice })) },
  });
  putSubmission(toSubmission(r.submission));
  return r;
}

// ---------- Thi online (ND-05) ----------

export interface AttemptPaper {
  attemptId: string;
  examId: string;
  title: string;
  startedAt: string;
  deadlineAt: string;
  sections: string[];
  questions: PaperQuestion[];
  answers: { questionId: string; choice: number }[];
}

const examIdOf = (id: string) => usePortalStore.getState().myExams.find((x) => x.exam.id === id)?.exam.id ?? id;

export async function startExam(classExamId: string) {
  const p = await api<AttemptPaper>(`/academic/portal/learning/exams/${examIdOf(classExamId)}/start`, { method: 'POST' });
  usePortalStore.setState((s) => ({
    myExams: s.myExams.map((x) => (x.exam.id === classExamId ? { ...x, attempt: 'in_progress', attemptId: p.attemptId } : x)),
  }));
  return p;
}

/** Tự lưu câu đã chọn (vị trí hiển thị; null = bỏ chọn) */
export const saveExamAnswers = (attemptId: string, answers: { questionId: string; choice: number | null }[]) =>
  api(`/academic/portal/learning/exam-attempts/${attemptId}/answers`, { method: 'PUT', body: { answers } });

export async function submitExam(classExamId: string, attemptId: string) {
  const r = await api<{ answered: number; total: number; autoSubmitted: boolean }>(`/academic/portal/learning/exam-attempts/${attemptId}/submit`, {
    method: 'POST',
  });
  usePortalStore.setState((s) => ({ myExams: s.myExams.map((x) => (x.exam.id === classExamId ? { ...x, attempt: 'submitted', canStart: false } : x)) }));
  return r;
}

export const examReview = (classExamId: string, studentId?: string) =>
  api<{ title: string; sections: string[]; questions: ReviewQuestion[] }>(
    `/academic/portal/learning/exams/${classExamId}/review${studentId ? `?studentId=${toGuid(studentId)}` : ''}`,
  );

// ---------- Đánh giá GV (ND-08) ----------

export interface ReviewSurvey {
  classId: string;
  classCode: string;
  className: string;
  period?: 'mid' | 'final' | null;
  teachers: { teacherId: string; name: string; reviewed: boolean }[];
}

export const loadSurveys = async () =>
  (await api<ReviewSurvey[]>('/academic/portal/learning/teacher-reviews/surveys')).map((s) => ({
    ...s,
    classId: fromGuid(s.classId),
    teachers: s.teachers.map((t) => ({ ...t, teacherId: fromGuid(t.teacherId) })),
  }));

export const submitTeacherReview = (v: {
  classId: string;
  teacherId: string;
  overall: number;
  ratings: TeacherReview['ratings'];
  comment?: string;
  anonymous: boolean;
}) =>
  api('/academic/portal/learning/teacher-reviews', {
    body: { ...v, classId: toGuid(v.classId), teacherId: toGuid(v.teacherId), comment: v.comment ?? null },
  });

// ---------- Flashcard ôn (ND-09) ----------

interface ServerFlashcards {
  decks: ServerDeck[];
  progress: { cardId: string; box: number; due: string; reviews: number; lapses: number }[];
  dueToday: number;
  streak: number;
}

function applyFlashcards(f: ServerFlashcards) {
  const progress: Record<string, CardProgress> = {};
  f.progress.forEach((p) => (progress[fromGuid(p.cardId)] = { box: p.box, due: p.due, reviews: p.reviews, lapses: p.lapses }));
  // Server trả chuỗi ngày học — dựng lại danh sách ngày liên tiếp cho thanh 14 ngày
  const today = dayjs();
  const studied = f.progress.length && f.streak ? Array.from({ length: f.streak }, (_, i) => today.subtract(i, 'day').format('YYYY-MM-DD')) : [];
  usePortalStore.setState({ decks: f.decks.map(toDeck), cardProgress: progress, studyDays: studied });
}

/** Gửi 1 lượt ôn (store đã cập nhật lạc quan ở trang) — server tính lại hộp / hạn ôn */
export async function reviewCard(cardId: string, remembered: boolean) {
  const deck = usePortalStore.getState().decks.find((d) => d.cards.some((c) => c.id === cardId));
  if (!deck) return;
  applyFlashcards(
    await api<ServerFlashcards>('/academic/portal/learning/flashcards/reviews', {
      body: { reviews: [{ deckId: toGuid(deck.id), cardId: toGuid(cardId), remembered }] },
    }),
  );
}

// ---------------- Tiện ích giao diện ----------------

/** Mở link tạm ở tab mới — mở tab ngay khi bấm (tránh chặn popup), điền địa chỉ khi server trả về */
export async function openInNewTab(get: () => Promise<string>) {
  const w = window.open('', '_blank');
  try {
    const url = await get();
    if (w) w.location.href = url;
    else window.open(url, '_blank', 'noopener');
  } catch (e) {
    w?.close();
    throw e;
  }
}

/** File chọn trong form (+ link) → tệp đính kèm đã tải lên server */
export async function uploadAttachments(items: { file?: File }[] = [], links: string[] = [], portal: 'staff' | 'teacher' | 'student' = 'staff') {
  const uploaded = await Promise.all(items.filter((f) => f.file).map((f) => uploadLearningFile(f.file!, portal)));
  return [...uploaded, ...links.filter(Boolean).map((url) => ({ name: url, kind: 'link' as const, url }))];
}
