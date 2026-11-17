/**
 * Nối trang Lộ trình với backend thật (/api/v1/academic/curricula — user story 09, LT-01 → LT-11).
 * Mỗi thao tác soạn nội dung trả về lộ trình đầy đủ mới nhất → thay nguyên object trong useCurriculumStore.
 * Tài liệu dạng file lưu ở kho riêng tư (ADR 0003): bấm mở → xin link tạm 15 phút (`openMaterial`).
 */
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, toGuid, toGuids } from '@/lib/api/ids';
import { loadCourses } from '../courses/courseApi';
import type { JlptLevel } from '../courses/types';
import { useCurriculumStore } from './curriculumStore';
import type { Curriculum, CurriculumStatus, CurriculumTier, ExamKind, MaterialType } from './types';

interface ServerMaterial {
  id: string;
  name: string;
  type: MaterialType;
  url?: string | null;
  isFile: boolean;
  fileName?: string | null;
  contentType?: string | null;
  size?: number | null;
  sharedWithStudents: boolean;
}

interface ServerCurriculum {
  id: string;
  code: string;
  name: string;
  level: JlptLevel;
  tier: CurriculumTier;
  entryRequirement?: string | null;
  version: string;
  previousVersionId?: string | null;
  textbook?: string | null;
  description?: string | null;
  tags: string[];
  status: CurriculumStatus;
  lessons: { id: string; title: string; hasQuiz: boolean; chapters: { id: string; title: string; materials: ServerMaterial[] }[] }[];
  exams: { id: string; kind: ExamKind; title: string; afterLessonId: string; note?: string | null }[];
  authorId: string;
  author: string;
  updatedAt: string;
  rowVersion: number;
}

/** Thông tin lộ trình nhập từ form (tạo / nhân bản / sửa) */
export interface CurriculumFormInput {
  code: string;
  name: string;
  level: JlptLevel;
  tier: CurriculumTier;
  entryRequirement?: string;
  version: string;
  textbook?: string;
  description?: string;
  tags?: string[];
}

const versions = new Map<string, number>();

function toCurriculum(c: ServerCurriculum): Curriculum {
  const id = fromGuid(c.id);
  versions.set(id, c.rowVersion);
  return {
    id,
    code: c.code,
    name: c.name,
    level: c.level,
    tier: c.tier,
    entryRequirement: c.entryRequirement ?? undefined,
    version: c.version,
    textbook: c.textbook ?? undefined,
    description: c.description ?? undefined,
    tags: c.tags,
    status: c.status,
    lessons: c.lessons.map((l) => ({
      id: l.id,
      title: l.title,
      hasQuiz: l.hasQuiz,
      chapters: l.chapters.map((ch) => ({
        id: ch.id,
        title: ch.title,
        materials: ch.materials.map((m) => ({
          id: m.id,
          name: m.name,
          type: m.type,
          url: m.url ?? '',
          isFile: m.isFile,
          size: m.size ?? undefined,
          sharedWithStudents: m.sharedWithStudents,
        })),
      })),
    })),
    exams: c.exams.map((e) => ({ id: e.id, kind: e.kind, title: e.title, afterLessonId: e.afterLessonId, note: e.note ?? undefined })),
    author: c.author || '—',
    updatedAt: c.updatedAt,
  };
}

function upsert(c: ServerCurriculum): Curriculum {
  const cur = toCurriculum(c);
  useCurriculumStore.setState((s) => ({
    curricula: s.curricula.some((x) => x.id === cur.id) ? s.curricula.map((x) => (x.id === cur.id ? cur : x)) : [cur, ...s.curricula],
  }));
  return cur;
}

/** Tải lộ trình (kèm nội dung). Không có quyền trang → giữ nguyên (các trang khác chỉ cần tên lộ trình) */
export async function loadCurricula() {
  try {
    const list = await api<ServerCurriculum[]>('/academic/curricula');
    useCurriculumStore.setState({ curricula: list.map(toCurriculum) });
  } catch (e) {
    if (!(e instanceof ApiError) || e.status !== 403) throw e;
  }
}

const infoBody = (v: CurriculumFormInput) => ({
  code: v.code,
  name: v.name.trim(),
  level: v.level,
  tier: v.tier,
  entryRequirement: v.tier === 'advanced' ? v.entryRequirement?.trim() || null : null,
  version: v.version,
  textbook: v.textbook?.trim() || null,
  description: v.description?.trim() || null,
  tags: v.tags ?? [],
});

const path = (id: string) => `/academic/curricula/${toGuid(id)}`;
const send = async (url: string, method: string, payload?: unknown) =>
  upsert(await api<ServerCurriculum>(url, payload === undefined ? { method } : { method, body: payload }));

/** LT-02 tạo · LT-03 nhân bản (bản nâng cao server tự gắn vào các khóa của bản gốc → tải lại khóa) */
export async function createCurriculum(v: CurriculumFormInput, cloneFromId?: string): Promise<Curriculum> {
  const created = upsert(
    await api<ServerCurriculum>('/academic/curricula', { body: { ...infoBody(v), cloneFromId: cloneFromId ? toGuid(cloneFromId) : null } }),
  );
  if (cloneFromId) await loadCourses().catch(() => undefined);
  return created;
}

/** LT-04 — bản đã phát hành chỉ đổi mô tả + thẻ */
export const updateCurriculumInfo = (id: string, v: CurriculumFormInput) =>
  send(path(id), 'PUT', { ...infoBody(v), rowVersion: versions.get(id) ?? 0 });

/** LT-09 — `setDefaultFor`: Id khóa đặt bản này làm mặc định */
export async function publishCurriculum(id: string, setDefaultFor: string[] = []) {
  await send(`${path(id)}/publish`, 'POST', { setDefaultFor: toGuids(setDefaultFor) });
  if (setDefaultFor.length) await loadCourses().catch(() => undefined);
}

/** LT-10 — bản nháp phiên bản kế tiếp, server gắn vào các khóa đang dùng bản cũ */
export async function newCurriculumVersion(id: string): Promise<Curriculum> {
  const draft = await send(`${path(id)}/new-version`, 'POST');
  await loadCourses().catch(() => undefined);
  return draft;
}

export const archiveCurriculum = (id: string) => send(`${path(id)}/archive`, 'POST');
export const restoreCurriculum = (id: string) => send(`${path(id)}/restore`, 'POST');

// ---------- Nội dung (LT-05, LT-06, LT-08) ----------

/** Thêm bài + các chapter mặc định (giống bản giả lập: Từ vựng · Ngữ pháp · Luyện nói) */
export async function addLesson(id: string, title: string, hasQuiz: boolean, chapters: string[] = []) {
  let cur = await send(`${path(id)}/lessons`, 'POST', { title, hasQuiz });
  const lessonId = cur.lessons[cur.lessons.length - 1].id;
  for (const t of chapters) cur = await send(`${path(id)}/lessons/${lessonId}/chapters`, 'POST', { title: t });
  return lessonId;
}

export const updateLesson = (id: string, lessonId: string, title: string, hasQuiz: boolean) =>
  send(`${path(id)}/lessons/${lessonId}`, 'PUT', { title, hasQuiz });
export const removeLesson = (id: string, lessonId: string) => send(`${path(id)}/lessons/${lessonId}`, 'DELETE');
export const reorderLessons = (id: string, ids: string[]) => send(`${path(id)}/lessons/order`, 'PUT', { ids });

export const addChapter = (id: string, lessonId: string, title: string) => send(`${path(id)}/lessons/${lessonId}/chapters`, 'POST', { title });
export const renameChapter = (id: string, lessonId: string, chapterId: string, title: string) =>
  send(`${path(id)}/lessons/${lessonId}/chapters/${chapterId}`, 'PUT', { title });
export const removeChapter = (id: string, lessonId: string, chapterId: string) =>
  send(`${path(id)}/lessons/${lessonId}/chapters/${chapterId}`, 'DELETE');

/** LT-06 — mỗi tệp 1 request (server kiểm đuôi + nội dung thật, ≤ 50 MB) */
export async function uploadMaterial(id: string, lessonId: string, chapterId: string, file: File, sharedWithStudents: boolean) {
  const form = new FormData();
  form.append('file', file);
  form.append('sharedWithStudents', String(sharedWithStudents));
  return upsert(await api<ServerCurriculum>(`${path(id)}/lessons/${lessonId}/chapters/${chapterId}/materials/file`, { body: form }));
}

export const addLinkMaterial = (id: string, lessonId: string, chapterId: string, v: { name: string; type: MaterialType; url: string; sharedWithStudents: boolean }) =>
  send(`${path(id)}/lessons/${lessonId}/chapters/${chapterId}/materials/link`, 'POST', v);
export const removeMaterial = (id: string, materialId: string) => send(`${path(id)}/materials/${materialId}`, 'DELETE');

export const addExam = (id: string, v: { kind: ExamKind; title: string; afterLessonId: string; note?: string }) =>
  send(`${path(id)}/exams`, 'POST', { ...v, note: v.note || null });
export const updateExam = (id: string, examId: string, v: { kind: ExamKind; title: string; afterLessonId: string; note?: string }) =>
  send(`${path(id)}/exams/${examId}`, 'PUT', { ...v, note: v.note || null });
export const removeExam = (id: string, examId: string) => send(`${path(id)}/exams/${examId}`, 'DELETE');

/** LT-07 — mở tài liệu file: xin link tạm từ server rồi mở tab mới (download = tải về đúng tên gốc) */
export async function openMaterial(id: string, materialId: string, download = false) {
  // Mở tab trước (trong cú bấm của người dùng) để trình duyệt không chặn popup, rồi điền link khi có
  const tab = window.open('', '_blank');
  try {
    const r = await api<{ url: string; expiresAt?: string }>(`${path(id)}/materials/${materialId}/link?download=${download}`);
    if (tab) tab.location.href = r.url;
    else window.location.href = r.url;
  } catch (e) {
    tab?.close();
    throw e;
  }
}
