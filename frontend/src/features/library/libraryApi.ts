/**
 * Thư viện tài liệu (user story 16, giai đoạn 23) — backend Library: /api/v1/library/documents, cổng HV / PH /portal/learning/library,
 * cổng GV /portal/teaching/library.
 *  - Trang nhân viên: danh sách đổ vào `useLibraryStore.docs` (thay dữ liệu giả); thêm / sửa / ẩn / quyền truy cập gọi API rồi thay bản server.
 *  - Xem tài liệu: mở lượt xem → link ký 10 phút (PDF / audio / video), watermark do server dựng; đóng trình xem → kết thúc lượt (tính phút đọc).
 *  - Danh sách không có "đọc nhiều nhất" — tải ở tab Thống kê.
 */
import type { QLevel, QSkill } from '@/features/academic/questions/types';
import { api } from '@/lib/api/http';
import { fromGuid, fromGuids, toGuid, toGuids } from '@/lib/api/ids';
import type { DocInput } from './libraryStore';
import { useLibraryStore } from './libraryStore';
import type { DocAudience, DocCategory, DocFileType, DocStatus, LibraryDoc } from './types';

interface ServerStats {
  views: number;
  readers: number;
  avgMinutes: number;
  lastViewedAt?: string | null;
  daily: number[];
  top: { name: string; role: string; views: number }[];
}

interface ServerDoc {
  id: string;
  title: string;
  subtitle?: string | null;
  description?: string | null;
  category: DocCategory;
  fileType: DocFileType;
  fileName?: string | null;
  size?: number | null;
  pages?: number | null;
  duration?: number | null;
  levels: string[];
  skills: QSkill[];
  tags: string[];
  publisher?: string | null;
  year?: number | null;
  audience?: DocAudience[];
  branchIds?: string[];
  watermark: boolean;
  allowDownload: boolean;
  status?: DocStatus;
  uploadedBy?: string;
  uploadedAt: string;
  url?: string | null;
  stats?: ServerStats;
  rowVersion?: number;
  /** Cổng: trùng cấp độ lớp đang học / dạy */
  matchesLevel?: boolean;
}

/** Lượt xem vừa mở (TV-05) */
export interface DocView {
  viewId: string;
  url: string;
  downloadUrl?: string;
  expiresAt?: string;
  /** Dòng in lặp trên trình xem; không có = không watermark */
  watermark?: string;
  allowDownload: boolean;
}

/** Tài liệu ở cổng HV / PH / GV — thêm cờ đúng cấp độ */
export type PortalDoc = LibraryDoc & { matchesLevel: boolean };

const versions = new Map<string, number>();
/** Link ngoài của tài liệu dạng link (để form sửa điền sẵn) */
const links = new Map<string, string>();

const LEVELS: QLevel[] = ['N5', 'N4', 'N3', 'N2', 'N1'];
const u = <T>(v: T | null | undefined) => v ?? undefined;
const noStats = (): LibraryDoc['stats'] => ({ views: 0, readers: 0, avgMinutes: 0, daily: Array(14).fill(0), top: [] });

function toDoc(d: ServerDoc): LibraryDoc {
  const id = fromGuid(d.id);
  if (d.rowVersion !== undefined) versions.set(id, d.rowVersion);
  if (d.url) links.set(id, d.url);
  return {
    id,
    title: d.title,
    subtitle: u(d.subtitle),
    description: u(d.description),
    category: d.category,
    fileType: d.fileType,
    fileName: u(d.fileName),
    size: u(d.size),
    pages: u(d.pages),
    duration: u(d.duration),
    levels: d.levels.filter((l): l is QLevel => LEVELS.includes(l as QLevel)),
    skills: d.skills,
    tags: d.tags,
    publisher: u(d.publisher),
    year: u(d.year),
    audience: d.audience ?? [],
    branchIds: d.branchIds?.length ? fromGuids(d.branchIds) : 'all',
    watermark: d.watermark,
    allowDownload: d.allowDownload,
    status: d.status ?? 'published',
    uploadedBy: d.uploadedBy ?? '',
    uploadedAt: d.uploadedAt,
    stats: d.stats ? { ...d.stats, avgMinutes: Number(d.stats.avgMinutes), lastViewedAt: u(d.stats.lastViewedAt) } : noStats(),
  };
}

/** Bản server mới → thay trong store; giữ "đọc nhiều nhất" đã tải (danh sách / thao tác trả top rỗng) */
function upsert(d: ServerDoc) {
  const doc = toDoc(d);
  useLibraryStore.setState((s) => {
    const old = s.docs.find((x) => x.id === doc.id);
    const merged = old && !doc.stats.top.length ? { ...doc, stats: { ...doc.stats, top: old.stats.top } } : doc;
    return { docs: old ? s.docs.map((x) => (x.id === doc.id ? merged : x)) : [merged, ...s.docs] };
  });
  return doc;
}

export const linkOf = (id: string) => links.get(id);

// ---------------- Trang nhân viên ----------------

const docs = '/library/documents';
const docPath = (id: string) => `${docs}/${toGuid(id)}`;

/** TV-01 — cả tài liệu ẩn khi người xem sửa được (lọc ẩn / hiện ở trang) */
export async function loadLibrary(includeHidden: boolean) {
  const list = await api<ServerDoc[]>(`${docs}${includeHidden ? '?includeHidden=true' : ''}`);
  useLibraryStore.setState({ docs: list.map(toDoc) });
}

const body = (v: DocInput, file?: { fileKey: string; fileName: string; size: number }, link?: string) => ({
  title: v.title,
  subtitle: v.subtitle ?? null,
  description: v.description ?? null,
  category: v.category,
  fileType: v.fileType,
  // Không gửi khóa tệp khi sửa mà không đổi tệp → server giữ tệp hiện tại
  fileKey: v.fileType === 'link' ? (link ?? null) : (file?.fileKey ?? null),
  fileName: v.fileType === 'link' ? null : (file?.fileName ?? null),
  size: v.fileType === 'link' ? null : (file?.size ?? null),
  levels: v.levels,
  skills: v.skills,
  tags: v.tags,
  pages: v.pages ?? null,
  duration: v.duration ?? null,
  publisher: v.publisher ?? null,
  year: v.year ?? null,
  audience: v.audience,
  branchIds: v.branchIds === 'all' ? [] : toGuids(v.branchIds),
});

async function upload(file: File) {
  const form = new FormData();
  form.append('file', file);
  return api<{ fileKey: string; fileName: string; size: number; fileType: DocFileType }>(`${docs}/files`, { body: form });
}

/** TV-02 — tải tệp lên kho (nếu có) rồi tạo tài liệu */
export async function createDoc(v: DocInput, file?: File, link?: string) {
  const stored = file ? await upload(file) : undefined;
  return upsert(await api<ServerDoc>(docs, { body: body(v, stored, link) }));
}

/** TV-03 — sửa thông tin; chọn tệp mới = đổi tệp, bỏ trống = giữ tệp cũ */
export async function updateDoc(id: string, v: DocInput, file?: File, link?: string) {
  const stored = file ? await upload(file) : undefined;
  return upsert(await api<ServerDoc>(docPath(id), { method: 'PUT', body: { ...body(v, stored, link), rowVersion: versions.get(id) ?? 0 } }));
}

/** TV-03 — ẩn / hiện */
export const setDocStatus = async (id: string, status: DocStatus) =>
  upsert(await api<ServerDoc>(`${docPath(id)}/${status === 'hidden' ? 'hide' : 'publish'}`, { method: 'POST' }));

/** TV-03 — xóa (vào Thùng rác) */
export async function deleteDoc(id: string) {
  await api(docPath(id), { method: 'DELETE' });
  useLibraryStore.setState((s) => ({ docs: s.docs.filter((d) => d.id !== id) }));
}

/** TV-04 — gửi đủ bộ: đối tượng, chi nhánh (rỗng = mọi chi nhánh), watermark, cho tải */
export async function setDocAccess(doc: LibraryDoc, patch: Partial<Pick<LibraryDoc, 'audience' | 'branchIds' | 'watermark' | 'allowDownload'>>) {
  const next = { ...doc, ...patch };
  return upsert(
    await api<ServerDoc>(`${docPath(doc.id)}/access`, {
      method: 'PUT',
      body: {
        audience: next.audience,
        branchIds: next.branchIds === 'all' ? [] : toGuids(next.branchIds),
        watermark: next.watermark,
        allowDownload: next.allowDownload,
        rowVersion: versions.get(doc.id) ?? 0,
      },
    }),
  );
}

/** TV-06 — số liệu đầy đủ (có "đọc nhiều nhất") */
export async function loadDocStats(id: string) {
  const s = await api<ServerStats>(`${docPath(id)}/stats`);
  useLibraryStore.setState((st) => ({
    docs: st.docs.map((d) => (d.id === id ? { ...d, stats: { ...s, avgMinutes: Number(s.avgMinutes), lastViewedAt: u(s.lastViewedAt) } } : d)),
  }));
}

interface ServerView {
  viewId: string;
  url: string;
  downloadUrl?: string | null;
  expiresAt?: string | null;
  watermark?: string | null;
  allowDownload: boolean;
  rowVersion?: number | null;
}

const toView = (v: ServerView): DocView => ({
  viewId: v.viewId,
  url: v.url,
  downloadUrl: u(v.downloadUrl),
  expiresAt: u(v.expiresAt),
  watermark: u(v.watermark),
  allowDownload: v.allowDownload,
});

/** TV-05 — trang nhân viên: mở lượt xem (số liệu tăng ở server → nhận phiên bản mới để sửa tiếp không bị 409) */
export async function startStaffView(id: string) {
  const v = await api<ServerView>(`${docPath(id)}/views`, { method: 'POST' });
  if (v.rowVersion != null) versions.set(id, v.rowVersion);
  useLibraryStore.setState((s) => ({
    docs: s.docs.map((d) => {
      if (d.id !== id) return d;
      const daily = [...d.stats.daily];
      daily[daily.length - 1] = (daily[daily.length - 1] ?? 0) + 1;
      return { ...d, stats: { ...d.stats, views: d.stats.views + 1, daily, lastViewedAt: new Date().toISOString() } };
    }),
  }));
  return toView(v);
}

/** Kết thúc lượt xem (đóng trình xem) — lỗi bỏ qua: server tự giới hạn phút đọc */
export const endStaffView = (viewId: string) => api(`/library/views/${viewId}/end`, { method: 'POST' }).catch(() => undefined);

// ---------------- Cổng HV / PH (TV-07) · GV (TV-08) ----------------

const portalBase = (asTeacher: boolean) => (asTeacher ? '/library/portal/teaching/library' : '/library/portal/learning/library');
const child = (studentId?: string) => (studentId ? `studentId=${toGuid(studentId)}` : '');

/** Tài liệu đang hiển thị, đúng đối tượng + chi nhánh; đúng cấp độ lớp đang học / dạy xếp trước */
export async function loadPortalLibrary(asTeacher: boolean, studentId?: string): Promise<PortalDoc[]> {
  const q = asTeacher ? '' : child(studentId);
  const list = await api<ServerDoc[]>(`${portalBase(asTeacher)}${q ? `?${q}` : ''}`);
  return list.map((d) => ({ ...toDoc(d), matchesLevel: Boolean(d.matchesLevel) }));
}

export async function startPortalView(id: string, asTeacher: boolean, studentId?: string) {
  const q = asTeacher ? '' : child(studentId);
  return toView(await api<ServerView>(`${portalBase(asTeacher)}/${toGuid(id)}/views${q ? `?${q}` : ''}`, { method: 'POST' }));
}

export const endPortalView = (viewId: string, asTeacher: boolean) =>
  api(`${portalBase(asTeacher)}/views/${viewId}/end`, { method: 'POST' }).catch(() => undefined);
