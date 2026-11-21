import { useCallback, useState } from 'react';
import { Radio } from 'antd';
import { FormField, ImportDialog, useNotify, type ImportColumn, type ImportRow, type RowCheck } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { matchOption } from '@/lib/spreadsheet';
import { normalizeText } from '@/lib/text';
import { Q_DIFFICULTY, Q_FORMATS, Q_LEVELS, Q_SKILLS } from './constants';
import { useQuestionStore, type QuestionInput } from './questionStore';
import { importQuestions } from './questionApi';
import { USE_API } from '@/lib/api/config';
import type { QDifficulty, QFormat, QSkill, QStatus } from './types';

const COLUMNS: ImportColumn[] = [
  { key: 'level', label: 'Cấp độ', required: true, aliases: ['Level', 'JLPT'], example: 'N4', hint: 'N5 … N1', width: 9 },
  { key: 'skill', label: 'Kỹ năng', required: true, aliases: ['Skill', 'Phần'], example: 'Ngữ pháp', hint: 'Từ vựng, Kanji, Ngữ pháp, Đọc hiểu, Nghe hiểu', width: 13 },
  { key: 'format', label: 'Dạng câu', aliases: ['Dạng', 'Format'], example: 'Chọn đáp án', hint: 'Chọn đáp án (mặc định) / Sắp xếp', width: 14 },
  { key: 'difficulty', label: 'Độ khó', aliases: ['Difficulty'], example: 'Trung bình', hint: 'Dễ / Trung bình / Khó', width: 12 },
  { key: 'stem', label: 'Đề bài', required: true, aliases: ['Câu hỏi', 'Nội dung', 'Question'], example: '雨{あめ}が＿＿、試合{しあい}は中止{ちゅうし}になった。', hint: 'furigana: 漢字{かんじ}', width: 44 },
  { key: 'passage', label: 'Đoạn văn', aliases: ['Bài đọc', 'Passage'], example: '', hint: 'bắt buộc với Đọc hiểu', width: 30 },
  { key: 'o1', label: 'Đáp án 1', required: true, aliases: ['A', 'Lựa chọn 1'], example: 'ふったので', width: 16 },
  { key: 'o2', label: 'Đáp án 2', required: true, aliases: ['B', 'Lựa chọn 2'], example: 'ふるのに', width: 16 },
  { key: 'o3', label: 'Đáp án 3', required: true, aliases: ['C', 'Lựa chọn 3'], example: 'ふっても', width: 16 },
  { key: 'o4', label: 'Đáp án 4', required: true, aliases: ['D', 'Lựa chọn 4'], example: 'ふるなら', width: 16 },
  { key: 'answer', label: 'Đáp án đúng', required: true, aliases: ['Đáp án', 'Answer', 'Key'], example: '1', hint: 'số 1–4 (hoặc A–D)', width: 12 },
  { key: 'explanation', label: 'Giải thích', aliases: ['Explanation', 'Lời giải'], example: '〜ので: chỉ nguyên nhân khách quan', width: 36 },
  { key: 'tags', label: 'Thẻ', aliases: ['Tags', 'Tag'], example: 'Ngữ pháp bài 12, Đề 2023', hint: 'cách nhau bởi dấu phẩy', width: 22 },
];

const SKILL_OPTS = (Object.keys(Q_SKILLS) as QSkill[]).map((k) => ({ value: k, label: Q_SKILLS[k].label, aliases: [Q_SKILLS[k].jp] }));
const FORMAT_OPTS = (Object.keys(Q_FORMATS) as QFormat[]).map((k) => ({ value: k, label: Q_FORMATS[k].label, aliases: k === 'star' ? ['Sắp xếp', '★', 'star'] : ['Trắc nghiệm', 'choice'] }));
const DIFF_OPTS = (Object.keys(Q_DIFFICULTY) as QDifficulty[]).map((k) => ({ value: k, label: Q_DIFFICULTY[k].label, aliases: k === 'medium' ? ['TB', 'Vừa'] : [] }));
const ANSWER: Record<string, number> = { '1': 0, '2': 1, '3': 2, '4': 3, a: 0, b: 1, c: 2, d: 3 };

/** Khóa so trùng: đề bài + 4 đáp án (bỏ dấu, khoảng trắng) */
const dupKey = (level: string, stem: string, options: string[]) => `${level}|${[stem, ...options].map((x) => normalizeText(x).replace(/\s+/g, '')).join('|')}`;

/** Nhập câu hỏi hàng loạt vào Ngân hàng câu hỏi — câu nhập vào luôn ở Nháp / Chờ duyệt, không bao giờ "Đã duyệt" ngay */
export function QuestionImportDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { user } = useAuth();
  const notify = useNotify();
  const questions = useQuestionStore((s) => s.questions);
  const add = useQuestionStore((s) => s.add);
  const [status, setStatus] = useState<Extract<QStatus, 'draft' | 'review'>>('review');

  const check = useCallback(
    (rows: ImportRow[]): RowCheck<QuestionInput>[] => {
      const existing = new Map(questions.filter((q) => q.status !== 'retired').map((q) => [dupKey(q.level, q.stem, q.options), q]));
      const seen = new Map<string, number>();
      return rows.map((r, i) => {
        const errors: string[] = [];
        const warnings: string[] = [];
        const level = Q_LEVELS.find((l) => l === r.level.toUpperCase().replace(/\s/g, ''));
        if (!level) errors.push(r.level ? `Cấp độ "${r.level}" không hợp lệ` : 'Thiếu cấp độ');
        const skill = matchOption(r.skill, SKILL_OPTS);
        if (!skill) errors.push(r.skill ? `Kỹ năng "${r.skill}" không hợp lệ` : 'Thiếu kỹ năng');
        const format = r.format ? matchOption(r.format, FORMAT_OPTS) : 'choice';
        if (!format) errors.push(`Dạng câu "${r.format}" không hợp lệ`);
        const difficulty = r.difficulty ? matchOption(r.difficulty, DIFF_OPTS) : 'medium';
        if (!difficulty) errors.push(`Độ khó "${r.difficulty}" không hợp lệ`);
        if (!r.stem) errors.push('Thiếu đề bài');
        if (format === 'star' && r.stem && !r.stem.includes('★')) errors.push('Câu sắp xếp phải có ký tự ★ trong đề');
        if (skill === 'reading' && !r.passage) errors.push('Câu đọc hiểu cần đoạn văn');
        const options = [r.o1, r.o2, r.o3, r.o4];
        const missingOpt = options.map((o, k) => (o ? undefined : k + 1)).filter(Boolean);
        if (missingOpt.length) errors.push(`Thiếu đáp án ${missingOpt.join(', ')}`);
        else if (new Set(options.map((o) => normalizeText(o))).size < 4) errors.push('Có 2 đáp án giống nhau');
        const answer = ANSWER[r.answer.trim().toLowerCase()];
        if (answer === undefined) errors.push(r.answer ? `Đáp án đúng "${r.answer}" phải là 1–4` : 'Thiếu đáp án đúng');
        if (skill === 'listening') warnings.push('Nghe hiểu: gắn file nghe sau khi nhập');

        let duplicate: string | undefined;
        if (level && r.stem && !missingOpt.length) {
          const key = dupKey(level, r.stem, options);
          const old = existing.get(key);
          if (old) duplicate = `Đã có câu ${old.code} giống hệt (đề + đáp án)`;
          else if (seen.has(key)) duplicate = `Trùng với dòng #${seen.get(key)} trong file`;
          else seen.set(key, i + 2);
        }

        return {
          errors,
          warnings,
          duplicate,
          value: errors.length
            ? undefined
            : {
                level: level!,
                skill: skill!,
                format: format!,
                difficulty: difficulty!,
                stem: r.stem,
                passage: r.passage || undefined,
                options,
                answer,
                explanation: r.explanation || undefined,
                lessonIds: [],
                tags: r.tags ? r.tags.split(/[,;]/).map((t) => t.trim()).filter(Boolean) : [],
              },
        };
      });
    },
    [questions],
  );

  return (
    <ImportDialog<QuestionInput>
      open={open}
      onClose={onClose}
      title="Nhập câu hỏi từ Excel"
      unitLabel="câu hỏi"
      templateName="mau-nhap-cau-hoi"
      columns={COLUMNS}
      check={check}
      previewKeys={['level', 'skill', 'stem', 'answer']}
      settings={
        <FormField label="Trạng thái sau khi nhập">
          <Radio.Group
            value={status}
            onChange={(e) => setStatus(e.target.value)}
            options={[
              { value: 'review', label: 'Gửi duyệt luôn' },
              { value: 'draft', label: 'Để nháp, soát lại sau' },
            ]}
          />
        </FormField>
      }
      notes="Câu nhập vào chưa gắn bài học — lọc “Chưa gắn bài” để gắn sau. Trùng = cùng cấp độ, đề bài và 4 đáp án với câu đang dùng."
      onImport={async (values) => {
        if (USE_API) {
          // 1 request; server kiểm lại từng câu. Số thứ tự = vị trí trong các dòng hợp lệ ở bước xem trước
          const r = await importQuestions(values.map((input, i) => ({ row: i + 1, input })), status === 'review');
          const notes = [
            r.draftListening ? `${r.draftListening} câu nghe hiểu để Nháp — tải file nghe rồi gửi duyệt.` : '',
            r.errors.length ? `${r.errors.length} câu bị từ chối: ${r.errors.slice(0, 3).map((e) => `câu ${e.row} — ${e.messages.join(', ')}`).join('; ')}` : '',
          ].filter(Boolean);
          notify.success({ title: `Đã nhập ${r.created} câu hỏi`, description: notes.join(' ') || (status === 'review' ? 'Đang chờ tổ chuyên môn duyệt.' : 'Ở trạng thái Nháp.') });
          return r.created;
        }
        await new Promise((r) => setTimeout(r, 500)); // giả lập gọi API
        values.forEach((v) => add(v, user?.fullName ?? 'Nhập từ Excel', status));
        notify.success({ title: `Đã nhập ${values.length} câu hỏi`, description: status === 'review' ? 'Đang chờ tổ chuyên môn duyệt.' : 'Ở trạng thái Nháp.' });
      }}
    />
  );
}
