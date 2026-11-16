import type { QLevel, QSkill } from '@/features/academic/questions/types';

/**
 * Thư viện tài liệu điện tử — KHÔNG gắn với bài học trong lộ trình (tài liệu giảng dạy nằm ở Lộ trình → Chapter).
 * Dùng cho: sách tham khảo, đề JLPT các năm, tài liệu tự học, file nghe / video luyện tập.
 *
 * Bảo vệ bản quyền: xem trực tuyến có WATERMARK tên người xem + thời gian, mặc định CHẶN tải về / in.
 * Thống kê: lượt xem, người đọc, thời gian đọc trung bình → biết tài liệu nào hữu ích.
 */

export type DocCategory = 'textbook' | 'jlpt' | 'reference' | 'self_study' | 'media';
export type DocFileType = 'pdf' | 'audio' | 'video' | 'link';
export type DocAudience = 'student' | 'teacher' | 'staff';
export type DocStatus = 'published' | 'hidden';

export interface LibraryDoc {
  id: string;
  title: string;
  /** Tên tiếng Nhật / phụ đề */
  subtitle?: string;
  description?: string;
  category: DocCategory;
  fileType: DocFileType;
  fileName?: string;
  /** Byte */
  size?: number;
  pages?: number;
  /** Phút — audio / video */
  duration?: number;
  levels: QLevel[];
  skills: QSkill[];
  tags: string[];
  publisher?: string;
  year?: number;
  /** Ai xem được */
  audience: DocAudience[];
  /** 'all' = mọi chi nhánh */
  branchIds: string[] | 'all';
  watermark: boolean;
  allowDownload: boolean;
  status: DocStatus;
  uploadedBy: string;
  uploadedAt: string;
  stats: {
    views: number;
    readers: number;
    /** Phút đọc trung bình / lượt */
    avgMinutes: number;
    lastViewedAt?: string;
    /** Lượt xem 14 ngày gần nhất (cũ → mới) */
    daily: number[];
    /** Người đọc nhiều nhất */
    top: { name: string; role: string; views: number }[];
  };
}
