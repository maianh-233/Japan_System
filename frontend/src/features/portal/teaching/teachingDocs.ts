import type { ClassSession } from '@/features/academic/classes/types';
import type { Lesson, Material } from '@/features/academic/curriculum/types';
import type { TeacherDoc } from '../types';

/** Loại tài liệu để chọn cách hiển thị trong khung giảng dạy */
export type TeachingDocKind = 'pdf' | 'doc' | 'ppt' | 'xls' | 'image' | 'audio' | 'video' | 'youtube' | 'drive' | 'link';

/** 1 tài liệu dùng trong buổi dạy — gộp từ lộ trình (học liệu của bài) và "Tài liệu của tôi" */
export interface TeachingDoc {
  id: string;
  name: string;
  kind: TeachingDocKind;
  url?: string;
  size?: number;
  source: 'curriculum' | 'mine';
  /** Nhóm hiển thị: tên phần của bài (lộ trình) hoặc "Tài liệu của tôi" */
  group: string;
  /** Chỉ GV thấy (giáo án, đáp án) */
  teacherOnly?: boolean;
  description?: string;
}

const isYoutube = (url?: string) => Boolean(url && /youtu\.?be/.test(url));

const kindFromName = (name: string): TeachingDocKind | undefined => {
  const ext = name.split('.').pop()?.toLowerCase() ?? '';
  if (ext === 'pdf') return 'pdf';
  if (['ppt', 'pptx', 'key'].includes(ext)) return 'ppt';
  if (['xls', 'xlsx', 'csv'].includes(ext)) return 'xls';
  if (['doc', 'docx'].includes(ext)) return 'doc';
  return undefined;
};

const fromMaterial = (m: Material, group: string): TeachingDoc => ({
  id: `M-${m.id}`,
  name: m.name,
  kind: m.type === 'video' && isYoutube(m.url) ? 'youtube' : m.type,
  url: m.url,
  size: m.size,
  source: 'curriculum',
  group,
  teacherOnly: !m.sharedWithStudents,
});

const fromTeacherDoc = (d: TeacherDoc): TeachingDoc => ({
  id: `T-${d.id}`,
  name: d.title,
  kind: d.kind === 'link' ? (isYoutube(d.url) ? 'youtube' : /drive\.google|docs\.google/.test(d.url ?? '') ? 'drive' : 'link') : d.kind === 'doc' ? (kindFromName(d.fileName) ?? 'doc') : d.kind,
  url: d.url,
  size: d.size,
  source: 'mine',
  group: d.sessionId ? 'Tài liệu của tôi — riêng buổi này' : 'Tài liệu của tôi',
  description: d.description,
});

/**
 * Tài liệu của buổi:
 *  1. Học liệu của bài trong lộ trình (theo từng phần)
 *  2. Tài liệu GV tự soạn: gắn riêng buổi này, hoặc gửi cho cả lớp mà không gắn buổi nào
 */
export function teachingDocsFor(session: ClassSession, lesson: Lesson | undefined, myDocs: TeacherDoc[]): TeachingDoc[] {
  const fromLesson = (lesson?.chapters ?? []).flatMap((c) => c.materials.map((m) => fromMaterial(m, c.title)));
  const mine = myDocs
    .filter((d) => d.classIds.includes(session.classId) && (d.sessionId === session.id || !d.sessionId))
    .sort((a, b) => Number(Boolean(b.sessionId)) - Number(Boolean(a.sessionId)))
    .map(fromTeacherDoc);
  return [...fromLesson, ...mine];
}

/** Link ngoài (http/https) — file demo nội bộ chưa có máy chủ thì không nhúng được */
export const isExternal = (url?: string): url is string => Boolean(url && /^(https?:|blob:)/.test(url));

/** URL nhúng vào iframe theo loại tài liệu; undefined = không nhúng được, hiện bản xem thử */
export function embedUrl(doc: TeachingDoc): string | undefined {
  const { url, kind } = doc;
  if (!isExternal(url)) return undefined;
  if (kind === 'youtube') {
    const id = url.match(/(?:youtu\.be\/|v=|embed\/)([\w-]{6,})/)?.[1];
    return id ? `https://www.youtube.com/embed/${id}?rel=0` : undefined;
  }
  if (kind === 'drive' || /docs\.google|drive\.google/.test(url)) {
    if (/\/folders\//.test(url)) return undefined;
    return url.replace(/\/(edit|view)(\?.*)?$/, '/preview');
  }
  if (kind === 'pdf') return url;
  // Office viewer chỉ đọc được link công khai (http/https), không đọc file vừa chọn trên máy (blob:)
  if ((kind === 'doc' || kind === 'ppt' || kind === 'xls') && /^https?:/.test(url)) return `https://view.officeapps.live.com/op/embed.aspx?src=${encodeURIComponent(url)}`;
  return undefined;
}
