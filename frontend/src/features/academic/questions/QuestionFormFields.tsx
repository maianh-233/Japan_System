import { Form, Radio } from 'antd';
import { Info } from 'lucide-react';
import { FileUpload, Select, TextArea, TextInput, type UploadItem } from '@/components/common';
import { useCurriculumStore } from '../curriculum/curriculumStore';
import { OPTION_LABELS, Q_DIFFICULTY, Q_FORMATS, Q_LEVELS, Q_SKILL_OPTIONS } from './constants';
import { QuestionCard } from './QuestionCard';
import type { QDifficulty, QFormat, QLevel, QSkill } from './types';

export interface QuestionFormValues {
  level: QLevel;
  skill: QSkill;
  format: QFormat;
  difficulty: QDifficulty;
  stem: string;
  passage?: string;
  audioFiles?: UploadItem[];
  transcript?: string;
  options: string[];
  answer: number;
  explanation?: string;
  curriculumId?: string;
  lessonIds?: string[];
  tags?: string[];
}

const TAG_SUGGEST = ['Đọc Kanji', 'Viết Kanji', 'Trợ từ', 'Kính ngữ', 'Sắp xếp câu', 'Dễ nhầm', 'Âm đục', 'Trường âm', 'Mẫu câu N3', 'Mẫu câu N2', 'Nghe bẫy số'];

/** Soạn câu hỏi: phân loại · đề bài (furigana) · đoạn văn / audio · 4 đáp án · giải thích · gắn bài học — có xem trước trực tiếp */
export function QuestionFormFields() {
  const form = Form.useFormInstance<QuestionFormValues>();
  const curricula = useCurriculumStore((s) => s.curricula);
  const level = Form.useWatch('level', form);
  const skill = Form.useWatch('skill', form);
  const format = Form.useWatch('format', form);
  const curriculumId = Form.useWatch('curriculumId', form);
  const stem = Form.useWatch('stem', form) ?? '';
  const options = Form.useWatch('options', form) ?? ['', '', '', ''];
  const answer = Form.useWatch('answer', form) ?? 0;
  const passage = Form.useWatch('passage', form);
  const audioFiles = Form.useWatch('audioFiles', form);

  const curriculum = curricula.find((c) => c.id === curriculumId);
  const curriculumOptions = curricula.filter((c) => c.status !== 'archived' && (!level || c.level === level)).map((c) => ({ value: c.id, label: `${c.name} (${c.version})` }));

  return (
    <>
      <Form.Item name="level" label="Cấp độ" rules={[{ required: true }]}>
        <Radio.Group
          optionType="button"
          buttonStyle="solid"
          options={Q_LEVELS.map((l) => ({ value: l, label: l }))}
          className="flex [&_.ant-radio-button-wrapper]:flex-1 [&_.ant-radio-button-wrapper]:text-center"
          onChange={() => form.setFieldsValue({ curriculumId: undefined, lessonIds: [] })}
        />
      </Form.Item>
      <Form.Item name="skill" label="Kỹ năng" rules={[{ required: true }]}>
        <Select options={Q_SKILL_OPTIONS} showSearch={false} allowClear={false} />
      </Form.Item>
      <Form.Item name="format" label="Dạng câu" rules={[{ required: true }]}>
        <Radio.Group options={(Object.keys(Q_FORMATS) as QFormat[]).map((k) => ({ value: k, label: Q_FORMATS[k].label }))} />
      </Form.Item>
      <Form.Item name="difficulty" label="Độ khó (dự kiến)" rules={[{ required: true }]}>
        <Radio.Group optionType="button" options={(Object.keys(Q_DIFFICULTY) as QDifficulty[]).map((k) => ({ value: k, label: Q_DIFFICULTY[k].label }))} />
      </Form.Item>

      {skill === 'reading' && (
        <Form.Item name="passage" label="Đoạn văn đọc hiểu" className="md:col-span-2" rules={[{ required: true, whitespace: true, message: 'Nhập đoạn văn' }]}>
          <TextArea rows={4} maxLength={2000} placeholder="最近{さいきん}、…" />
        </Form.Item>
      )}
      {skill === 'listening' && (
        <>
          <Form.Item name="audioFiles" label="File nghe" className="md:col-span-2" rules={[{ required: true, type: 'array', min: 1, message: 'Tải file nghe (mp3)' }]}>
            <FileUpload kind="audio" maxFiles={1} maxSizeMB={20} placeholder="Kéo thả file mp3 / m4a vào đây" />
          </Form.Item>
          <Form.Item name="transcript" label="Lời thoại (chỉ GV xem)" className="md:col-span-2">
            <TextArea rows={3} maxLength={2000} placeholder={'男：…\n女：…'} />
          </Form.Item>
        </>
      )}

      <Form.Item
        name="stem"
        label="Đề bài"
        className="md:col-span-2"
        rules={[{ required: true, whitespace: true, message: 'Nhập đề bài' }]}
        extra={
          <span className="inline-flex items-start gap-1.5">
            <Info size={14} className="mt-0.5 shrink-0" aria-hidden />
            <span>
              Furigana: <code>漢字{'{かんじ}'}</code> · Từ được hỏi: <code>**学校**</code> · Chỗ trống: <code>（　　）</code>
              {format === 'star' && (
                <>
                  {' '}
                  · Sắp xếp: <code>＿＿　＿＿　★　＿＿</code>
                </>
              )}
            </span>
          </span>
        }
      >
        <TextArea rows={2} maxLength={500} placeholder="わたしは　毎朝{まいあさ}　コーヒー（　　）飲{の}みます。" />
      </Form.Item>

      <Form.Item name="answer" label={format === 'star' ? '4 mảnh câu — chọn mảnh ở vị trí ★' : 'Đáp án (chọn đáp án đúng)'} className="md:col-span-2" rules={[{ required: true }]}>
        <Radio.Group className="!grid w-full grid-cols-1 gap-2 sm:grid-cols-2">
          {OPTION_LABELS.map((l, i) => (
            <div key={l} className="flex items-center gap-2 rounded-lg border border-line px-2 py-1.5">
              <Radio value={i} aria-label={`Đáp án ${l} đúng`} />
              <span className="w-4 text-xs font-semibold text-ink-muted">{l}</span>
              <Form.Item name={['options', i]} noStyle rules={[{ required: true, whitespace: true, message: `Nhập đáp án ${l}` }]}>
                <TextInput size="small" placeholder={`Đáp án ${l}`} className="flex-1" />
              </Form.Item>
            </div>
          ))}
        </Radio.Group>
      </Form.Item>

      <Form.Item name="explanation" label="Giải thích (hiện cho HV sau khi nộp)" className="md:col-span-2">
        <TextArea rows={2} maxLength={1000} placeholder="Vì sao đáp án này đúng, các đáp án nhiễu sai ở đâu..." />
      </Form.Item>

      <Form.Item name="curriculumId" label="Lộ trình">
        <Select options={curriculumOptions} placeholder="Chọn lộ trình cùng cấp độ" onChange={() => form.setFieldValue('lessonIds', [])} />
      </Form.Item>
      <Form.Item name="lessonIds" label="Bài học" extra="Dùng để bốc câu theo bài khi giao bài tập / tạo đề.">
        <Select
          mode="multiple"
          disabled={!curriculum}
          placeholder={curriculum ? 'Chọn bài' : 'Chọn lộ trình trước'}
          options={(curriculum?.lessons ?? []).map((l, i) => ({ value: l.id, label: `Bài ${i + 1}: ${l.title}` }))}
          optionFilterProp="label"
        />
      </Form.Item>
      <Form.Item name="tags" label="Thẻ" className="md:col-span-2">
        <Select mode="tags" placeholder="Thêm thẻ để lọc nhanh" options={TAG_SUGGEST.map((t) => ({ value: t, label: t }))} />
      </Form.Item>

      {/* Xem trước */}
      <div className="md:col-span-2">
        <p className="mb-2 text-[13px] font-medium text-ink-secondary">Xem trước (như học viên thấy)</p>
        <div className="rounded-[var(--radius-card)] border border-dashed border-primary-200 bg-primary-50/30 p-4">
          <QuestionCard
            q={{
              stem,
              options,
              answer,
              passage: skill === 'reading' ? passage : undefined,
              audio: skill === 'listening' && audioFiles?.length ? { name: audioFiles[0].name, durationSec: 60 } : undefined,
              format: format ?? 'choice',
            }}
            showAnswer
            compact
          />
        </div>
      </div>
    </>
  );
}
