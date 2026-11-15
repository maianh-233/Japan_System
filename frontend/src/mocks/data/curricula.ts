import dayjs from 'dayjs';
import type { JlptLevel } from '@/features/academic/courses/types';
import type { CourseCurriculum, Curriculum, CurriculumStatus, CurriculumTier, Exam, Lesson, Material, MaterialType } from '@/features/academic/curriculum/types';

/**
 * Dữ liệu giả — Lộ trình + liên kết Khóa ↔ Lộ trình.
 * Có: lộ trình chuẩn (mặc định) + nâng cao trong cùng khóa, dùng chung nhiều khóa, bản cũ lưu trữ, bản nháp,
 * thi giữa kỳ / cuối kỳ, tài liệu đủ loại (pdf, word, ppt, excel, audio, video, Drive).
 */

const ago = (days: number) => dayjs().subtract(days, 'day').hour(10).minute(0).second(0).toISOString();

const CHAPTER_SETS = [
  ['Từ vựng', 'Ngữ pháp', 'Luyện nói'],
  ['Từ vựng & Kanji', 'Ngữ pháp', 'Đọc hiểu', 'Nghe hiểu'],
  ['Hội thoại mẫu', 'Mẫu câu', 'Đóng vai'],
];

/** Tài liệu mẫu theo loại chapter */
function materialsFor(prefix: string, chapter: string, lessonNo: number, seed: number): Material[] {
  const t = chapter.toLowerCase();
  const pick: [MaterialType, string, boolean][] = t.includes('nghe')
    ? [
        ['audio', `Audio nghe bài ${lessonNo}.mp3`, true],
        ['pdf', `Script nghe bài ${lessonNo}.pdf`, false],
      ]
    : t.includes('ngữ pháp') || t.includes('mẫu câu')
      ? [
          ['ppt', `Slide ngữ pháp bài ${lessonNo}.pptx`, false],
          ['doc', `Bài tập ngữ pháp bài ${lessonNo}.docx`, true],
          ['video', `Video giải thích mẫu câu bài ${lessonNo}`, true],
        ]
      : t.includes('từ vựng')
        ? [
            ['xls', `Danh sách từ vựng bài ${lessonNo}.xlsx`, true],
            ['drive', `Flashcard từ vựng bài ${lessonNo} (Drive)`, true],
          ]
        : t.includes('đọc')
          ? [['pdf', `Bài đọc bài ${lessonNo}.pdf`, true]]
          : [
              ['ppt', `Slide tình huống bài ${lessonNo}.pptx`, false],
              ['link', `Hướng dẫn hoạt động lớp bài ${lessonNo}`, false],
            ];
  // Bớt ngẫu nhiên 1 tài liệu để dữ liệu không đều tăm tắp
  const list = seed % 4 === 0 && pick.length > 1 ? pick.slice(0, -1) : pick;
  return list.map(([type, name, shared], i) => ({
    id: `${prefix}-M${i + 1}`,
    name,
    type,
    url: type === 'drive' ? 'https://drive.google.com/drive/folders/demo' : type === 'video' ? 'https://youtu.be/demo' : type === 'link' ? 'https://docs.google.com/document/d/demo' : `/files/demo/${encodeURIComponent(name)}`,
    size: ['drive', 'video', 'link'].includes(type) ? undefined : 150_000 + ((seed * 7919 + i * 104729) % 4_800_000),
    sharedWithStudents: shared,
  }));
}

function lessons(prefix: string, topics: string[], chapterSet = 0): Lesson[] {
  return topics.map((t, i) => ({
    id: `${prefix}-L${i + 1}`,
    title: t,
    hasQuiz: (i + 1) % 3 === 0,
    chapters: CHAPTER_SETS[chapterSet].map((c, j) => ({
      id: `${prefix}-L${i + 1}-C${j + 1}`,
      title: c,
      materials: materialsFor(`${prefix}-L${i + 1}-C${j + 1}`, c, i + 1, i + j),
    })),
  }));
}

/** Mốc thi: giữa kỳ sau bài giữa, cuối kỳ sau bài cuối (đề tạo sau từ Ngân hàng câu hỏi) */
function exams(prefix: string, ls: Lesson[], opts: { midterm?: boolean } = {}): Exam[] {
  const { midterm = true } = opts;
  const list: Exam[] = [];
  if (midterm && ls.length >= 6) list.push({ id: `${prefix}-EX-MID`, kind: 'midterm', title: 'Kiểm tra giữa kỳ', afterLessonId: ls[Math.floor(ls.length / 2) - 1].id });
  list.push({ id: `${prefix}-EX-FIN`, kind: 'final', title: 'Thi cuối kỳ', afterLessonId: ls[ls.length - 1].id, note: midterm && ls.length >= 6 ? 'Tổng hợp toàn khóa, trọng tâm nửa sau' : undefined });
  return list;
}

const N5_TOPICS = ['Giới thiệu bản thân', 'Đồ vật xung quanh', 'Địa điểm', 'Thời gian', 'Đi lại', 'Ăn uống', 'Cho và nhận', 'Tính từ', 'Sở thích', 'Tồn tại', 'Số đếm', 'So sánh', 'Mong muốn', 'Thể て', 'Thể ない', 'Thể từ điển'];
const N5_ADV_TOPICS = [...N5_TOPICS.slice(0, 12), 'Thể て & thể ない', 'Thể từ điển & thể た', 'Luyện đề N5 (1)', 'Luyện đề N5 (2)', 'Kaiwa mở rộng'];
const N4_TOPICS = ['Thể thông thường', 'Thể khả năng', 'Thể ý chí', 'Câu điều kiện', 'Thể bị động', 'Thể sai khiến', 'Kính ngữ cơ bản', 'Cho nhận nâng cao', 'Phỏng đoán', 'Truyền đạt', 'Mục đích', 'Trạng thái'];
const N3_TOPICS = ['Diễn tả nguyên nhân', 'Diễn tả thời điểm', 'Phạm vi & giới hạn', 'Truyền văn', 'Đọc hiểu đoạn ngắn', 'Đọc hiểu đoạn trung', 'Nghe hiểu điểm chính', 'Nghe hiểu khái quát', 'Luyện đề tổng hợp 1', 'Luyện đề tổng hợp 2'];
const N3_ADV_TOPICS = ['Ngữ pháp N3 cấp tốc (1)', 'Ngữ pháp N3 cấp tốc (2)', 'Đọc hiểu đoạn trung', 'Đọc hiểu đoạn dài', 'Nghe hiểu điểm chính', 'Nghe hiểu tức thời', 'Làm quen ngữ pháp N2', 'Luyện đề tổng hợp 1', 'Luyện đề tổng hợp 2', 'Luyện đề tổng hợp 3'];
const N2_TOPICS = ['Ngữ pháp N2 (1)', 'Ngữ pháp N2 (2)', 'Ngữ pháp N2 (3)', 'Đọc hiểu báo chí', 'Đọc hiểu văn bản dài', 'Nghe hiểu tức thời', 'Nghe hiểu tổng hợp', 'Luyện đề JLPT N2'];
const N1_TOPICS = ['Ngữ pháp N1 (1)', 'Ngữ pháp N1 (2)', 'Đọc hiểu bình luận', 'Nghe hiểu bài giảng', 'Luyện đề JLPT N1'];
const KAIWA_TOPICS = ['Chào hỏi & làm quen', 'Mua sắm', 'Nhà hàng', 'Hỏi đường', 'Gọi điện thoại', 'Kể chuyện cuối tuần', 'Bày tỏ ý kiến', 'Phàn nàn & xin lỗi'];
const KIDS_TOPICS = ['Hiragana あ–そ', 'Hiragana た–ほ', 'Hiragana ま–ん', 'Gia đình', 'Con vật', 'Màu sắc', 'Trường học', 'Lễ hội Nhật Bản'];
const XKLD_TOPICS = ['Chào hỏi nơi làm việc', 'Báo cáo – liên lạc – thảo luận', 'An toàn lao động', 'Từ vựng ngành thực phẩm', 'Từ vựng ngành xây dựng', 'Sinh hoạt ký túc xá', 'Quy tắc đổ rác', 'Đi khám bệnh'];
const INTERVIEW_TOPICS = ['Giới thiệu bản thân khi phỏng vấn', 'Lý do muốn sang Nhật', 'Câu hỏi về kinh nghiệm', 'Tác phong & cúi chào', 'Phỏng vấn thử'];
const BIZ_TOPICS = ['Kính ngữ trong công ty', 'Viết email công việc', 'Họp & thuyết trình', 'Đàm phán', 'Văn hóa công sở Nhật'];

interface Seed {
  id: string;
  code: string;
  name: string;
  level: JlptLevel;
  tier: CurriculumTier;
  version: string;
  status: CurriculumStatus;
  author: string;
  topics: string[];
  tags: string[];
  set?: number;
  age: number;
  textbook?: string;
  entryRequirement?: string;
  description?: string;
  midterm?: boolean;
}

const seeds: Seed[] = [
  { id: 'CUR-N5-V2', code: 'LT-N5', name: 'Lộ trình N5 — Minna no Nihongo I', level: 'N5', tier: 'standard', version: 'v2.0', textbook: 'Minna no Nihongo I (bài 1–25)', status: 'published', author: 'Trần Mai Anh', topics: N5_TOPICS, tags: ['JLPT', 'Minna no Nihongo'], age: 40 },
  { id: 'CUR-N5-ADV', code: 'LT-N5-ADV', name: 'Lộ trình N5 nâng cao', level: 'N5', tier: 'advanced', version: 'v1.0', textbook: 'Minna no Nihongo I + đề JLPT N5', status: 'published', author: 'Trần Mai Anh', topics: N5_ADV_TOPICS, tags: ['JLPT', 'Luyện đề', 'Cấp tốc'], age: 35, entryRequirement: 'Đã thuộc bảng chữ cái; điểm test đầu vào TB lớp ≥ 80%', description: 'Gộp nhanh phần ngữ pháp cơ bản, dành 3 buổi cuối luyện đề N5 và Kaiwa mở rộng.' },
  { id: 'CUR-N5-V1', code: 'LT-N5', name: 'Lộ trình N5 — Minna no Nihongo I', level: 'N5', tier: 'standard', version: 'v1.3', textbook: 'Minna no Nihongo I (bản cũ)', status: 'archived', author: 'Trần Mai Anh', topics: N5_TOPICS.slice(0, 14), tags: ['JLPT', 'Minna no Nihongo'], age: 300 },
  { id: 'CUR-N4-V2', code: 'LT-N4', name: 'Lộ trình N4 — Minna no Nihongo II', level: 'N4', tier: 'standard', version: 'v2.1', textbook: 'Minna no Nihongo II (bài 26–50)', status: 'published', author: 'Phạm Đức Long', topics: N4_TOPICS, tags: ['JLPT', 'Minna no Nihongo'], set: 1, age: 60 },
  { id: 'CUR-N3-V1', code: 'LT-N3', name: 'Lộ trình N3 — Shin Kanzen + Mimikara', level: 'N3', tier: 'standard', version: 'v1.2', textbook: 'Shin Kanzen Master N3, Mimikara Oboeru N3', status: 'published', author: 'Nguyễn Thu Hà', topics: N3_TOPICS, tags: ['JLPT', 'Shin Kanzen'], set: 1, age: 90 },
  { id: 'CUR-N3-ADV', code: 'LT-N3-ADV', name: 'Lộ trình N3 nâng cao — hướng N2', level: 'N3', tier: 'advanced', version: 'v1.0', textbook: 'Shin Kanzen Master N3 + N2 (phần đầu)', status: 'published', author: 'Nguyễn Thu Hà', topics: N3_ADV_TOPICS, tags: ['JLPT', 'Shin Kanzen', 'Luyện đề'], set: 1, age: 50, entryRequirement: 'Đã đỗ N4 hoặc điểm test đầu vào TB lớp ≥ 85%', description: 'Nhịp nhanh hơn bản chuẩn, thêm 1 bài làm quen ngữ pháp N2 và 3 buổi luyện đề.' },
  { id: 'CUR-N3-V2', code: 'LT-N3', name: 'Lộ trình N3 — Shin Kanzen + Mimikara', level: 'N3', tier: 'standard', version: 'v2.0', textbook: 'Shin Kanzen Master N3 (bổ sung đề 2025)', status: 'draft', author: 'Nguyễn Thu Hà', topics: [...N3_TOPICS, 'Luyện đề tổng hợp 3'], tags: ['JLPT', 'Shin Kanzen'], set: 1, age: 3 },
  { id: 'CUR-N2', code: 'LT-N2', name: 'Lộ trình N2 tổng hợp', level: 'N2', tier: 'standard', version: 'v1.0', textbook: 'Shin Kanzen Master N2', status: 'published', author: 'Nguyễn Thu Hà', topics: N2_TOPICS, tags: ['JLPT', 'Shin Kanzen', 'Online'], set: 1, age: 120 },
  { id: 'CUR-N1', code: 'LT-N1', name: 'Lộ trình N1', level: 'N1', tier: 'standard', version: 'v0.3', status: 'draft', author: 'Nguyễn Thu Hà', topics: N1_TOPICS, tags: ['JLPT'], set: 1, age: 4, midterm: false },
  { id: 'CUR-KAIWA', code: 'LT-KW-B1', name: 'Kaiwa B1 — giao tiếp tình huống', level: 'N3', tier: 'standard', version: 'v1.1', status: 'published', author: 'Sato Yuki', topics: KAIWA_TOPICS, tags: ['Giao tiếp'], set: 2, age: 70, midterm: false },
  { id: 'CUR-KIDS', code: 'LT-KIDS', name: 'Tiếng Nhật thiếu nhi', level: 'N5', tier: 'standard', version: 'v1.0', status: 'published', author: 'Trần Mai Anh', topics: KIDS_TOPICS, tags: ['Thiếu nhi'], set: 2, age: 150 },
  { id: 'CUR-XKLD', code: 'LT-XKLD', name: 'Tiếng Nhật đi làm / XKLĐ', level: 'N4', tier: 'standard', version: 'v1.4', status: 'published', author: 'Võ Thị Hạnh', topics: XKLD_TOPICS, tags: ['XKLĐ', 'Cấp tốc'], set: 2, age: 45 },
  { id: 'CUR-INTERVIEW', code: 'LT-PV', name: 'Luyện phỏng vấn đơn hàng', level: 'N4', tier: 'standard', version: 'v1.0', status: 'published', author: 'Võ Thị Hạnh', topics: INTERVIEW_TOPICS, tags: ['XKLĐ', 'Giao tiếp'], set: 2, age: 80, midterm: false },
  { id: 'CUR-BIZ', code: 'LT-BIZ', name: 'Tiếng Nhật thương mại', level: 'N2', tier: 'standard', version: 'v1.0', status: 'published', author: 'Sato Yuki', topics: BIZ_TOPICS, tags: ['Doanh nghiệp', 'Giao tiếp'], set: 2, age: 260, midterm: false },
];

export const MOCK_CURRICULA: Curriculum[] = seeds.map(({ topics, set, age, midterm, ...c }) => {
  const ls = lessons(c.id, topics, set);
  return { ...c, lessons: ls, exams: exams(c.id, ls, { midterm }), updatedAt: ago(age) };
});

const link = (courseId: string, curriculumId: string, isDefault: boolean, age: number): CourseCurriculum => ({ courseId, curriculumId, isDefault, linkedAt: ago(age) });

export const MOCK_COURSE_CURRICULA: CourseCurriculum[] = [
  link('CRS-01', 'CUR-N5-V2', true, 40),
  link('CRS-01', 'CUR-N5-ADV', false, 35), // lớp đầu vào tốt
  link('CRS-01', 'CUR-N5-V1', false, 380),
  link('CRS-13', 'CUR-N5-V2', true, 40), // N5 online dùng chung lộ trình với N5 offline
  link('CRS-02', 'CUR-N4-V2', true, 60),
  link('CRS-03', 'CUR-N3-V1', true, 90),
  link('CRS-03', 'CUR-N3-ADV', false, 50),
  link('CRS-03', 'CUR-N3-V2', false, 3),
  link('CRS-09', 'CUR-N3-V1', true, 85), // N3 1-1 dùng chung với N3 lớp nhóm
  link('CRS-04', 'CUR-N2', true, 120),
  link('CRS-10', 'CUR-N2', true, 55),
  link('CRS-05', 'CUR-KAIWA', true, 70),
  link('CRS-08', 'CUR-KAIWA', true, 65),
  link('CRS-06', 'CUR-KIDS', true, 150),
  link('CRS-07', 'CUR-XKLD', true, 45),
  link('CRS-14', 'CUR-INTERVIEW', true, 70),
  link('CRS-14', 'CUR-XKLD', false, 60),
  link('CRS-11', 'CUR-N1', true, 4),
  link('CRS-12', 'CUR-BIZ', true, 260),
];
