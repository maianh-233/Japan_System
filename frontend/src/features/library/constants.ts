import { BookOpen, ClipboardList, FileAudio, FileText, FileVideo, GraduationCap, Library, Link2, NotebookPen, Users, UserRound, Briefcase, type LucideIcon } from 'lucide-react';
import type { DocAudience, DocCategory, DocFileType } from './types';

export const DOC_CATEGORIES: Record<DocCategory, { label: string; icon: LucideIcon; /** màu bìa */ cover: string }> = {
  textbook: { label: 'Giáo trình', icon: BookOpen, cover: 'from-primary-600 to-primary-800' },
  jlpt: { label: 'Đề JLPT các năm', icon: ClipboardList, cover: 'from-danger-600 to-danger-700' },
  reference: { label: 'Sách tham khảo', icon: Library, cover: 'from-success-600 to-success-700' },
  self_study: { label: 'Tự học', icon: NotebookPen, cover: 'from-warning-600 to-warning-700' },
  media: { label: 'Nghe / Video', icon: FileAudio, cover: 'from-primary-400 to-primary-700' },
};
export const DOC_CATEGORY_OPTIONS = (Object.keys(DOC_CATEGORIES) as DocCategory[]).map((k) => ({ value: k, label: DOC_CATEGORIES[k].label }));

export const DOC_FILE_TYPES: Record<DocFileType, { label: string; icon: LucideIcon }> = {
  pdf: { label: 'PDF', icon: FileText },
  audio: { label: 'Audio', icon: FileAudio },
  video: { label: 'Video', icon: FileVideo },
  link: { label: 'Liên kết', icon: Link2 },
};
export const DOC_FILE_TYPE_OPTIONS = (Object.keys(DOC_FILE_TYPES) as DocFileType[]).map((k) => ({ value: k, label: DOC_FILE_TYPES[k].label }));

export const DOC_AUDIENCE: Record<DocAudience, { label: string; icon: LucideIcon }> = {
  student: { label: 'Học viên', icon: GraduationCap },
  teacher: { label: 'Giáo viên', icon: UserRound },
  staff: { label: 'Nhân viên', icon: Briefcase },
};
export const DOC_AUDIENCE_OPTIONS = (Object.keys(DOC_AUDIENCE) as DocAudience[]).map((k) => ({ value: k, label: DOC_AUDIENCE[k].label }));

export const AUDIENCE_ICON = Users;
