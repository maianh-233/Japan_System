/**
 * Nối Ngân hàng câu hỏi với backend thật (/api/v1/academic/questions — user story 11, NH-01 → NH-08).
 * Mỗi thao tác trả câu hỏi mới nhất → thay trong useQuestionStore. Duyệt / gửi duyệt / ngừng dùng chạy hàng loạt 1 request:
 * server bỏ qua câu không hợp lệ (sai trạng thái, tự duyệt…) kèm lý do.
 * File nghe lưu kho riêng tư (ADR 0003): nghe thử → xin link tạm (`audioLink`).
 */
import { api } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useQuestionStore, type QuestionInput } from './questionStore';
import type { QDifficulty, QFormat, QLevel, QSkill, QStatus, Question } from './types';

interface ServerQuestion {
  id: string;
  code: string;
  level: QLevel;
  skill: QSkill;
  format: QFormat;
  difficulty: QDifficulty;
  stem: string;
  passage?: string | null;
  audio?: { name: string; durationSec: number } | null;
  transcript?: string | null;
  options: string[];
  answer: number;
  explanation?: string | null;
  curriculumId?: string | null;
  lessonIds: string[];
  tags: string[];
  status: QStatus;
  authorId: string;
  author: string;
  createdAt: string;
  updatedAt: string;
  review?: { byId: string; by: string; at: string; note?: string | null } | null;
  stats: { used: number; attempts: number; correct: number; picks?: number[] | null };
  rowVersion: number;
}

export interface BulkOutcome {
  done: number;
  /** Câu bị bỏ qua: mã + lý do (sai trạng thái, tự duyệt…) */
  skipped: { code?: string; reason: string }[];
}

export type QuestionAction = 'submit' | 'approve' | 'return' | 'retire' | 'reopen';

/** Id nhân viên soạn từng câu — giao diện so với người đang đăng nhập (ẩn nút "Duyệt" câu của chính mình) */
export const questionAuthors = new Map<string, string>();
const versions = new Map<string, number>();

const toQuestion = (q: ServerQuestion): Question => {
  const id = fromGuid(q.id);
  versions.set(id, q.rowVersion);
  questionAuthors.set(id, fromGuid(q.authorId));
  return {
    id,
    code: q.code,
    level: q.level,
    skill: q.skill,
    format: q.format,
    difficulty: q.difficulty,
    stem: q.stem,
    passage: q.passage ?? undefined,
    audio: q.audio ?? undefined,
    transcript: q.transcript ?? undefined,
    options: q.options,
    answer: q.answer,
    explanation: q.explanation ?? undefined,
    curriculumId: q.curriculumId ? fromGuid(q.curriculumId) : undefined,
    lessonIds: q.lessonIds,
    tags: q.tags,
    status: q.status,
    author: q.author,
    createdAt: q.createdAt,
    updatedAt: q.updatedAt,
    review: q.review ? { by: q.review.by, at: q.review.at, note: q.review.note ?? undefined } : undefined,
    stats: { used: q.stats.used, attempts: q.stats.attempts, correct: q.stats.correct, picks: q.stats.picks ?? undefined },
  };
};

/** Thay / thêm các câu trong store (câu mới lên đầu) */
function upsert(list: ServerQuestion[]) {
  const incoming = list.map(toQuestion);
  useQuestionStore.setState((s) => {
    const byId = new Map(incoming.map((q) => [q.id, q]));
    const kept = s.questions.map((q) => byId.get(q.id) ?? q);
    const fresh = incoming.filter((q) => !s.questions.some((x) => x.id === q.id));
    return { questions: [...fresh, ...kept] };
  });
  return incoming;
}

const body = (input: QuestionInput) => ({
  level: input.level,
  skill: input.skill,
  format: input.format,
  difficulty: input.difficulty,
  stem: input.stem,
  passage: input.passage ?? null,
  transcript: input.transcript ?? null,
  options: input.options,
  answer: input.answer,
  explanation: input.explanation ?? null,
  curriculumId: input.curriculumId ? toGuid(input.curriculumId) : null,
  lessonIds: input.lessonIds,
  tags: input.tags,
});

/** Toàn bộ ngân hàng trong phạm vi quyền (thay danh sách trong store) */
export async function loadQuestions() {
  const list = await api<ServerQuestion[]>('/academic/questions');
  useQuestionStore.setState({ questions: list.map(toQuestion) });
}

export async function createQuestion(input: QuestionInput, submit: boolean): Promise<Question> {
  return upsert([await api<ServerQuestion>('/academic/questions', { body: { ...body(input), submit } })])[0];
}

export async function updateQuestion(id: string, input: QuestionInput): Promise<Question> {
  return upsert([
    await api<ServerQuestion>(`/academic/questions/${toGuid(id)}`, { method: 'PUT', body: { ...body(input), rowVersion: versions.get(id) ?? 0 } }),
  ])[0];
}

/** Độ dài file nghe (giây) — trình duyệt đọc metadata; đọc không được → 60 */
export function audioDuration(file: File): Promise<number> {
  return new Promise((resolve) => {
    const url = URL.createObjectURL(file);
    const audio = new Audio();
    const done = (sec: number) => {
      URL.revokeObjectURL(url);
      resolve(Math.max(1, Math.min(1800, Math.round(sec))));
    };
    audio.preload = 'metadata';
    audio.onloadedmetadata = () => done(Number.isFinite(audio.duration) ? audio.duration : 60);
    audio.onerror = () => done(60);
    audio.src = url;
  });
}

export async function uploadAudio(id: string, file: File): Promise<Question> {
  const form = new FormData();
  form.append('file', file);
  form.append('durationSec', String(await audioDuration(file)));
  return upsert([await api<ServerQuestion>(`/academic/questions/${toGuid(id)}/audio`, { body: form })])[0];
}

/** Link nghe thử 15 phút */
export const audioLink = async (id: string) => (await api<{ url: string }>(`/academic/questions/${toGuid(id)}/audio-link`)).url;

export async function changeStatus(action: QuestionAction, ids: string[], note?: string): Promise<BulkOutcome> {
  const r = await api<{ done: ServerQuestion[]; skipped: { id: string; code?: string | null; reason: string }[] }>(`/academic/questions/${action}`, {
    body: { ids: ids.map(toGuid), note: note ?? null },
  });
  upsert(r.done);
  return { done: r.done.length, skipped: r.skipped.map((s) => ({ code: s.code ?? undefined, reason: s.reason })) };
}

export async function duplicateQuestion(id: string): Promise<Question> {
  return upsert([await api<ServerQuestion>(`/academic/questions/${toGuid(id)}/duplicate`, { method: 'POST' })])[0];
}

export interface ImportOutcome {
  created: number;
  errors: { row: number; messages: string[] }[];
  /** Câu nghe hiểu tạo ở dạng nháp (cần tải file nghe) */
  draftListening: number;
}

/** Nhập nhiều câu 1 request — `rows[i].row` = số dòng trong file để báo lỗi */
export async function importQuestions(rows: { row: number; input: QuestionInput }[], submit: boolean): Promise<ImportOutcome> {
  const r = await api<{ created: ServerQuestion[]; errors: { row: number; messages: string[] }[]; draftListening: number }>('/academic/questions/import', {
    body: { submit, rows: rows.map((x) => ({ row: x.row, ...body(x.input) })) },
  });
  upsert(r.created);
  return { created: r.created.length, errors: r.errors, draftListening: r.draftListening };
}
