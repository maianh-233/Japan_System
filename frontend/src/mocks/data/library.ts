import dayjs from 'dayjs';
import type { LibraryDoc } from '@/features/library/types';

/**
 * Dữ liệu giả — Thư viện tài liệu. Tình huống: tài liệu đọc nhiều, tài liệu chưa ai đọc, tài liệu đang ẩn,
 * tài liệu chỉ dành cho GV, file nghe / video, tài liệu cho phép tải (đề mẫu công khai).
 */

const ago = (d: number) => dayjs().subtract(d, 'day').hour(9).toISOString();
const READERS = [
  { name: 'Nguyễn Văn An', role: 'Học viên N3-K12' },
  { name: 'Lê Hoàng Phúc', role: 'Học viên N2-K05' },
  { name: 'Hoàng Thu Giang', role: 'Học viên N4-K15' },
  { name: 'Đỗ Minh Tuấn', role: 'Học viên XKLĐ-K08' },
  { name: 'Nguyễn Thu Hà', role: 'Giáo viên' },
  { name: 'Phạm Đức Long', role: 'Giáo viên' },
];

/** Lượt xem 14 ngày: dạng sóng theo hệ số */
const daily = (base: number, seed: number) => Array.from({ length: 14 }, (_, i) => Math.max(0, Math.round(base * (0.6 + 0.4 * Math.sin((i + seed) / 2)) + ((i * seed) % 3))));

type Seed = Omit<LibraryDoc, 'id' | 'stats' | 'uploadedAt' | 'status' | 'watermark' | 'allowDownload' | 'branchIds'> &
  Partial<Pick<LibraryDoc, 'status' | 'watermark' | 'allowDownload' | 'branchIds'>> & { views: number; age: number };

const seeds: Seed[] = [
  { title: 'Minna no Nihongo I — Bản dịch & giải thích ngữ pháp', subtitle: 'みんなの日本語 初級I 翻訳・文法解説', category: 'textbook', fileType: 'pdf', pages: 196, size: 18_400_000, levels: ['N5'], skills: ['grammar', 'vocab'], tags: ['Minna no Nihongo', 'Bản dịch'], publisher: '3A Corporation', year: 2012, audience: ['student', 'teacher'], uploadedBy: 'Trần Mai Anh', views: 1240, age: 300 },
  { title: 'Minna no Nihongo II — Bản dịch & giải thích ngữ pháp', subtitle: 'みんなの日本語 初級II', category: 'textbook', fileType: 'pdf', pages: 210, size: 19_900_000, levels: ['N4'], skills: ['grammar', 'vocab'], tags: ['Minna no Nihongo', 'Bản dịch'], publisher: '3A Corporation', year: 2013, audience: ['student', 'teacher'], uploadedBy: 'Phạm Đức Long', views: 860, age: 280 },
  { title: 'Đề thi JLPT N3 — Tháng 7/2025 (bản giải chi tiết)', subtitle: '日本語能力試験 N3 2025年7月', category: 'jlpt', fileType: 'pdf', pages: 48, size: 6_200_000, levels: ['N3'], skills: ['vocab', 'kanji', 'grammar', 'reading'], tags: ['Đề thật', 'Có giải'], year: 2025, audience: ['student', 'teacher'], uploadedBy: 'Nguyễn Thu Hà', views: 2310, age: 90 },
  { title: 'Đề thi JLPT N3 — Tháng 7/2025: File nghe', category: 'jlpt', fileType: 'audio', duration: 40, size: 38_000_000, fileName: 'N3-2025-07-choukai.mp3', levels: ['N3'], skills: ['listening'], tags: ['Đề thật', 'Nghe'], year: 2025, audience: ['student', 'teacher'], uploadedBy: 'Nguyễn Thu Hà', views: 1580, age: 90 },
  { title: 'Đề thi JLPT N2 — Tháng 12/2024', subtitle: '日本語能力試験 N2 2024年12月', category: 'jlpt', fileType: 'pdf', pages: 56, size: 7_100_000, levels: ['N2'], skills: ['vocab', 'kanji', 'grammar', 'reading'], tags: ['Đề thật'], year: 2024, audience: ['student', 'teacher'], uploadedBy: 'Nguyễn Thu Hà', views: 940, age: 200 },
  { title: 'Đề mẫu JLPT N5 chính thức (Japan Foundation)', subtitle: '日本語能力試験 N5 問題例', category: 'jlpt', fileType: 'pdf', pages: 32, size: 3_400_000, levels: ['N5'], skills: ['vocab', 'grammar', 'reading', 'listening'], tags: ['Đề mẫu', 'Công khai'], publisher: 'Japan Foundation / JEES', audience: ['student', 'teacher', 'staff'], uploadedBy: 'Trần Mai Anh', views: 1720, age: 400, allowDownload: true, watermark: false },
  { title: 'Shin Kanzen Master N3 — Ngữ pháp', subtitle: '新完全マスター 文法 N3', category: 'reference', fileType: 'pdf', pages: 176, size: 21_000_000, levels: ['N3'], skills: ['grammar'], tags: ['Shin Kanzen'], publisher: '3A Corporation', year: 2011, audience: ['student', 'teacher'], uploadedBy: 'Nguyễn Thu Hà', views: 1410, age: 150 },
  { title: 'Mimikara Oboeru N3 — Từ vựng', subtitle: '耳から覚える 語彙トレーニング N3', category: 'reference', fileType: 'pdf', pages: 312, size: 26_500_000, levels: ['N3'], skills: ['vocab'], tags: ['Mimikara'], publisher: 'アルク', audience: ['student', 'teacher'], uploadedBy: 'Nguyễn Thu Hà', views: 990, age: 140 },
  { title: '512 Kanji N5–N4 kèm cách nhớ', category: 'self_study', fileType: 'pdf', pages: 84, size: 9_800_000, levels: ['N5', 'N4'], skills: ['kanji'], tags: ['Kanji', 'Mẹo nhớ'], publisher: 'JP Center (biên soạn)', audience: ['student', 'teacher'], uploadedBy: 'Trần Mai Anh', views: 2050, age: 60 },
  { title: 'Sổ tay 100 mẫu câu phỏng vấn đơn hàng', category: 'self_study', fileType: 'pdf', pages: 40, size: 2_100_000, levels: ['N4'], skills: ['vocab', 'listening'], tags: ['XKLĐ', 'Phỏng vấn'], publisher: 'JP Center (biên soạn)', audience: ['student', 'teacher', 'staff'], uploadedBy: 'Võ Thị Hạnh', views: 760, age: 45, branchIds: ['hc'] },
  { title: 'Luyện nghe hội thoại công sở (video)', category: 'media', fileType: 'video', duration: 95, size: 420_000_000, levels: ['N3', 'N2'], skills: ['listening'], tags: ['Kaiwa', 'Công sở'], audience: ['student', 'teacher'], uploadedBy: 'Sato Yuki', views: 430, age: 30 },
  { title: 'NHK News Easy — tuyển chọn bài đọc', category: 'media', fileType: 'link', levels: ['N4', 'N3'], skills: ['reading'], tags: ['Báo dễ'], publisher: 'NHK', audience: ['student'], uploadedBy: 'Nguyễn Thu Hà', views: 310, age: 20 },
  { title: 'Giáo án mẫu & đáp án Minna I (nội bộ GV)', category: 'textbook', fileType: 'pdf', pages: 120, size: 8_400_000, levels: ['N5'], skills: ['grammar', 'vocab'], tags: ['Giáo án', 'Nội bộ'], audience: ['teacher'], uploadedBy: 'Trần Mai Anh', views: 180, age: 120 },
  { title: 'Đề luyện đọc hiểu N1 (bản thảo)', category: 'self_study', fileType: 'pdf', pages: 64, size: 5_000_000, levels: ['N1'], skills: ['reading'], tags: ['Đọc hiểu'], audience: ['student', 'teacher'], uploadedBy: 'Sato Yuki', views: 0, age: 4, status: 'hidden' },
  { title: 'Từ vựng chuyên ngành thực phẩm (XKLĐ)', category: 'self_study', fileType: 'pdf', pages: 28, size: 1_600_000, levels: ['N4'], skills: ['vocab'], tags: ['XKLĐ', 'Thực phẩm'], audience: ['student', 'teacher'], uploadedBy: 'Võ Thị Hạnh', views: 0, age: 9, branchIds: ['hc'] },
];

export const MOCK_LIBRARY: LibraryDoc[] = seeds.map(({ views, age, ...d }, i) => {
  const readers = Math.round(views / (2.2 + (i % 3)));
  return {
    ...d,
    id: `DOC-${i + 1}`,
    status: d.status ?? 'published',
    watermark: d.watermark ?? true,
    allowDownload: d.allowDownload ?? false,
    branchIds: d.branchIds ?? 'all',
    uploadedAt: ago(age),
    stats: {
      views,
      readers,
      avgMinutes: d.fileType === 'pdf' ? 8 + (i % 7) * 3 : d.duration ? Math.round(d.duration * 0.6) : 4,
      lastViewedAt: views ? dayjs().subtract((i * 7) % 30, 'hour').toISOString() : undefined,
      daily: views ? daily(Math.max(1, views / Math.max(30, age)), i + 1) : Array(14).fill(0),
      top: views ? READERS.slice(i % 3, (i % 3) + 4).map((r, k) => ({ ...r, views: Math.max(1, Math.round(views / (40 + k * 25))) })) : [],
    },
  };
});
