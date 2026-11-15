import {
  Archive,
  CheckCircle2,
  FileAudio,
  FilePen,
  FileSpreadsheet,
  FileText,
  FileType2,
  FileVideo,
  Flag,
  HardDrive,
  Link2,
  Medal,
  Presentation,
  Rocket,
  Sprout,
  type LucideIcon,
} from 'lucide-react';
import type { Tone } from '@/components/common';
import type { CurriculumStatus, CurriculumTier, ExamKind, MaterialType } from './types';

type Meta = { label: string; tone: Tone; icon: LucideIcon };

export const CURRICULUM_STATUS: Record<CurriculumStatus, Meta> = {
  published: { label: 'Đã phát hành', tone: 'success', icon: CheckCircle2 },
  draft: { label: 'Bản nháp', tone: 'warning', icon: FilePen },
  archived: { label: 'Đã lưu trữ', tone: 'neutral', icon: Archive },
};
export const CURRICULUM_STATUS_OPTIONS = (Object.keys(CURRICULUM_STATUS) as CurriculumStatus[]).map((k) => ({ value: k, label: CURRICULUM_STATUS[k].label }));

export const TIERS: Record<CurriculumTier, Meta & { hint: string }> = {
  standard: { label: 'Chuẩn', tone: 'info', icon: Sprout, hint: 'Dùng cho đa số lớp — thường là lộ trình mặc định của khóa' },
  advanced: { label: 'Nâng cao', tone: 'primary', icon: Rocket, hint: 'Cho lớp có đầu vào tốt: nhịp nhanh hơn, thêm nội dung mở rộng / luyện đề' },
};
export const TIER_OPTIONS = (Object.keys(TIERS) as CurriculumTier[]).map((k) => ({ value: k, label: TIERS[k].label }));

export const EXAM_KINDS: Record<ExamKind, Meta> = {
  midterm: { label: 'Thi giữa kỳ', tone: 'warning', icon: Flag },
  final: { label: 'Thi cuối kỳ', tone: 'danger', icon: Medal },
};

/** Loại tài liệu: nhãn + icon + màu chữ (Tailwind) + là file hay link */
export const MATERIAL_TYPES: Record<MaterialType, { label: string; icon: LucideIcon; color: string; kind: 'file' | 'link'; ext?: string[] }> = {
  pdf: { label: 'PDF', icon: FileText, color: 'text-danger-600', kind: 'file', ext: ['pdf'] },
  doc: { label: 'Word', icon: FileType2, color: 'text-primary-600', kind: 'file', ext: ['doc', 'docx'] },
  ppt: { label: 'PowerPoint', icon: Presentation, color: 'text-warning-600', kind: 'file', ext: ['ppt', 'pptx'] },
  xls: { label: 'Excel', icon: FileSpreadsheet, color: 'text-success-600', kind: 'file', ext: ['xls', 'xlsx', 'csv'] },
  audio: { label: 'Audio', icon: FileAudio, color: 'text-primary-700', kind: 'file', ext: ['mp3', 'wav', 'm4a'] },
  video: { label: 'Video', icon: FileVideo, color: 'text-danger-700', kind: 'link' },
  drive: { label: 'Google Drive', icon: HardDrive, color: 'text-success-700', kind: 'link' },
  link: { label: 'Liên kết', icon: Link2, color: 'text-ink-secondary', kind: 'link' },
};
export const MATERIAL_TYPE_OPTIONS = (Object.keys(MATERIAL_TYPES) as MaterialType[]).map((k) => ({ value: k, label: MATERIAL_TYPES[k].label }));

/** Đoán loại tài liệu từ tên file */
export function materialTypeOf(fileName: string): MaterialType {
  const ext = fileName.split('.').pop()?.toLowerCase() ?? '';
  return (Object.keys(MATERIAL_TYPES) as MaterialType[]).find((k) => MATERIAL_TYPES[k].ext?.includes(ext)) ?? 'pdf';
}

/** Gợi ý tag hay dùng */
export const TAG_SUGGESTIONS = ['JLPT', 'Luyện đề', 'Minna no Nihongo', 'Shin Kanzen', 'Giao tiếp', 'Thiếu nhi', 'XKLĐ', 'Online', 'Cấp tốc', 'Doanh nghiệp'];
