import { useEffect, useState } from 'react';
import { Checkbox, Form, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { Plus, RefreshCw, Shuffle, Trash2 } from 'lucide-react';
import { Button, DateInput, ModalForm, NumberInput, Select, TextInput, useNotify } from '@/components/common';
import { OPTION_LABELS } from '@/features/academic/questions/constants';
import { Furigana } from '@/features/academic/questions/Furigana';
import { cn } from '@/lib/cn';
import { QUIZ_SKILL_OPTIONS } from '../constants';
import * as learningApi from '../learningApi';
import type { ClassExam, ClassInfo, ExamMode, QuizSkill } from '../types';

interface SectionValues {
  name: string;
  skill?: QuizSkill;
  points: number;
  count: number;
}

interface Values {
  title: string;
  mode: ExamMode;
  startsAt: Dayjs;
  durationMinutes: number;
  passPercent: number;
  shuffle: boolean;
  sections: SectionValues[];
}

const DEFAULT_SECTIONS: SectionValues[] = [
  { name: 'Từ vựng', skill: 'vocab', points: 1, count: 20 },
  { name: 'Ngữ pháp', skill: 'grammar', points: 2, count: 15 },
  { name: 'Đọc hiểu', skill: 'reading', points: 2, count: 5 },
];

/** Khóa so sánh: đổi tên / kỹ năng / số câu của phần → đề xem trước không còn đúng */
const specOf = (list: SectionValues[] = []) => JSON.stringify(list.map((s) => [s.skill ?? null, s.count]));

/**
 * Soạn kỳ thi từ 1 mốc thi của lộ trình (ND-04): hình thức online / giấy, giờ thi, thời lượng, % đạt, các phần.
 * "Bốc đề" lấy câu ĐÃ DUYỆT trong phạm vi bài của mốc (server), xem trước kèm đáp án, đổi từng câu; lưu = nháp (đề còn sửa được tới khi phát hành).
 * Kỳ thi đã phát hành chỉ đổi giờ thi (khi chưa ai làm bài).
 */
export function ExamComposer({
  cls,
  milestone,
  exam,
  open,
  onClose,
}: {
  cls: ClassInfo;
  milestone: { id: string; title: string };
  exam?: ClassExam;
  open: boolean;
  onClose: () => void;
}) {
  const notify = useNotify();
  const [form] = Form.useForm<Values>();
  const sections = Form.useWatch('sections', form);
  const [preview, setPreview] = useState<{ spec: string; sections: learningApi.ExamPaperSection[] } | null>(null);
  const [drawing, setDrawing] = useState(false);
  const published = exam?.status === 'published';

  // Sửa nháp: nạp đề đang lưu làm bản xem trước
  useEffect(() => {
    setPreview(null);
    if (!open || !exam) return;
    void learningApi
      .examPaper(exam.id)
      .then((p) => setPreview({ spec: specOf(exam.sections.map((s) => ({ name: s.name, skill: s.skill, points: s.points, count: s.questionCount }))), sections: p.sections }))
      .catch(() => undefined);
  }, [open, exam]);

  const stale = preview && preview.spec !== specOf(sections);

  const draw = async () => {
    const list = (form.getFieldValue('sections') as SectionValues[] | undefined) ?? [];
    if (!list.length) return notify.warning({ title: 'Thêm ít nhất 1 phần thi' });
    setDrawing(true);
    try {
      const drawn = await learningApi.drawExam(cls.id, milestone.id, list.map((s) => ({ name: s.name, skill: s.skill, points: s.points, count: s.count })));
      setPreview({ spec: specOf(list), sections: drawn });
    } catch (e) {
      notify.error({ title: (e as Error).message });
    } finally {
      setDrawing(false);
    }
  };

  /** Đổi 1 câu: bốc 1 câu cùng kỹ năng, khác mọi câu đang có trong đề */
  const swap = async (k: number, q: number) => {
    if (!preview) return;
    const sec = preview.sections[k];
    const current = preview.sections.flatMap((s) => s.questions.map((x) => x.id));
    try {
      const [one] = await learningApi.drawExam(cls.id, milestone.id, [{ name: sec.name, skill: sec.skill ?? undefined, points: sec.points, count: 1 }], current);
      const next = preview.sections.map((s, i) => (i === k ? { ...s, questions: s.questions.map((x, j) => (j === q ? one.questions[0] : x)) } : s));
      setPreview({ ...preview, sections: next });
    } catch (e) {
      notify.error({ title: (e as Error).message });
    }
  };

  const initialValues: Partial<Values> = exam
    ? {
        title: exam.title,
        mode: exam.mode,
        startsAt: dayjs(exam.startsAt),
        durationMinutes: exam.durationMinutes,
        passPercent: exam.passPercent,
        shuffle: exam.shuffle,
        sections: exam.sections.map((s) => ({ name: s.name, skill: s.skill, points: s.points, count: s.questionCount })),
      }
    : { title: milestone.title, mode: 'online', startsAt: dayjs().add(7, 'day').hour(18).minute(0), durationMinutes: 60, passPercent: 60, shuffle: true, sections: DEFAULT_SECTIONS };

  return (
    <ModalForm<Values>
      form={form}
      key={`${milestone.id}-${exam?.id ?? 'new'}-${open}`}
      open={open}
      onClose={onClose}
      width={820}
      title={exam ? (published ? `Đổi giờ thi — ${exam.title}` : `Sửa kỳ thi — ${exam.title}`) : `Tạo kỳ thi — ${milestone.title}`}
      description={`${cls.code} · câu hỏi bốc từ Ngân hàng (đã duyệt) trong phạm vi bài của mốc thi`}
      submitAction="save"
      submitText={published ? 'Đổi giờ' : 'Lưu nháp'}
      successMessage={published ? 'Đã đổi giờ thi' : 'Đã lưu kỳ thi (nháp) — kiểm tra rồi bấm Phát hành'}
      initialValues={initialValues}
      onSubmit={async (v) => {
        const list = published ? exam!.sections.map((s) => ({ name: s.name, skill: s.skill, points: s.points, count: s.questionCount })) : v.sections;
        const usePreview = !published && preview && !stale;
        const input: learningApi.ExamInput = {
          examId: milestone.id,
          title: published ? exam!.title : v.title,
          mode: published ? exam!.mode : v.mode,
          startsAt: v.startsAt.toISOString(),
          durationMinutes: v.durationMinutes,
          passPercent: published ? exam!.passPercent : v.passPercent,
          shuffle: published ? exam!.shuffle : v.shuffle,
          sections: list.map((s, k) => ({
            name: s.name,
            skill: s.skill,
            points: s.points,
            ...(usePreview ? { questionIds: preview!.sections[k].questions.map((q) => q.id) } : published ? { questionIds: exam!.sections[k].questionIds } : { count: s.count }),
          })),
        };
        if (exam) await learningApi.updateExam(exam.id, input);
        else await learningApi.createExam(cls.id, input);
      }}
    >
      {!published && (
        <>
          <Form.Item name="title" label="Tên kỳ thi" rules={[{ required: true, whitespace: true, message: 'Nhập tên' }]}>
            <TextInput maxLength={200} />
          </Form.Item>
          <Form.Item name="mode" label="Hình thức">
            <Radio.Group
              optionType="button"
              options={[
                { value: 'online', label: 'Online trên Cổng HV' },
                { value: 'paper', label: 'Giấy, thi tại lớp' },
              ]}
            />
          </Form.Item>
        </>
      )}
      <div className="grid grid-cols-1 gap-x-4 sm:grid-cols-3">
        <Form.Item name="startsAt" label="Bắt đầu" rules={[{ required: true, message: 'Chọn giờ thi' }]}>
          <DateInput showTime format="HH:mm DD/MM/YYYY" />
        </Form.Item>
        <Form.Item name="durationMinutes" label="Thời lượng" rules={[{ required: true, message: 'Nhập thời lượng' }]}>
          <NumberInput min={10} max={240} step={5} unit="phút" />
        </Form.Item>
        {!published && (
          <Form.Item name="passPercent" label="Đạt từ" rules={[{ required: true }]}>
            <NumberInput min={1} max={100} unit="%" />
          </Form.Item>
        )}
      </div>

      {!published && (
        <>
          <Form.Item name="shuffle" valuePropName="checked" className="!mb-3">
            <Checkbox>Thi online: mỗi học viên một đề (đảo câu trong phần + đảo đáp án)</Checkbox>
          </Form.Item>

          <p className="mb-2 text-sm font-medium text-ink">Các phần thi</p>
          <Form.List name="sections">
            {(fields, { add, remove }) => (
              <div className="mb-3 flex flex-col gap-2">
                {fields.map((f) => (
                  <div key={f.key} className="grid grid-cols-[1fr_140px_100px_100px_auto] items-start gap-2 max-sm:grid-cols-2">
                    <Form.Item name={[f.name, 'name']} rules={[{ required: true, whitespace: true, message: 'Tên phần' }]} className="!mb-0">
                      <TextInput placeholder="Tên phần" />
                    </Form.Item>
                    <Form.Item name={[f.name, 'skill']} className="!mb-0">
                      <Select placeholder="Mọi kỹ năng" options={QUIZ_SKILL_OPTIONS} />
                    </Form.Item>
                    <Form.Item name={[f.name, 'count']} rules={[{ required: true, message: 'Số câu' }]} className="!mb-0">
                      <NumberInput min={1} max={100} unit="câu" />
                    </Form.Item>
                    <Form.Item name={[f.name, 'points']} rules={[{ required: true, message: 'Điểm' }]} className="!mb-0">
                      <NumberInput min={0.25} max={100} step={0.25} unit="đ/câu" />
                    </Form.Item>
                    <Button icon={Trash2} iconOnly variant="ghost" tone="danger" aria-label="Xóa phần" onClick={() => remove(f.name)} disabled={fields.length === 1} />
                  </div>
                ))}
                <Button icon={Plus} size="sm" variant="soft" className="self-start" onClick={() => add({ name: '', points: 1, count: 10 })}>
                  Thêm phần
                </Button>
              </div>
            )}
          </Form.List>
          <p className="mb-3 text-[13px] text-ink-secondary">
            Tổng: <strong>{(sections ?? []).reduce((n, s) => n + (s?.count ?? 0), 0)}</strong> câu ·{' '}
            <strong>{(sections ?? []).reduce((n, s) => n + (s?.count ?? 0) * (s?.points ?? 0), 0)}</strong> điểm
          </p>

          <div className="mb-2 flex flex-wrap items-center gap-2">
            <Button icon={Shuffle} variant="outline" loading={drawing} onClick={() => void draw()}>
              {preview ? 'Bốc lại đề' : 'Bốc đề & xem trước'}
            </Button>
            <span className={cn('text-xs', stale ? 'text-warning-700' : 'text-ink-muted')}>
              {stale
                ? 'Đã đổi các phần — đề xem trước không còn khớp, bốc lại hoặc lưu để hệ thống tự bốc.'
                : preview
                  ? 'Lưu sẽ dùng đúng các câu đang xem trước.'
                  : 'Không xem trước thì hệ thống tự bốc khi lưu.'}
            </span>
          </div>
          {preview && !stale && (
            <div className="flex max-h-[45vh] flex-col gap-3 overflow-y-auto rounded-lg border border-line bg-canvas p-3">
              {preview.sections.map((s, k) => (
                <section key={k}>
                  <p className="mb-1 text-[13px] font-semibold text-ink">
                    {s.name} · {s.questions.length} câu × {s.points} đ
                  </p>
                  <ol className="flex flex-col gap-1.5">
                    {s.questions.map((q, j) => (
                      <li key={q.id} className="flex items-start gap-2 rounded-md bg-surface px-2.5 py-1.5 text-[13px]">
                        <span className="tabular w-6 shrink-0 text-ink-muted">{j + 1}.</span>
                        <span className="min-w-0 flex-1">
                          <Furigana text={q.stem} />
                          <span className="block text-xs text-success-700">
                            {OPTION_LABELS[q.answer]}. <Furigana text={q.options[q.answer] ?? ''} /> <span className="text-ink-muted">· {q.code}</span>
                          </span>
                        </span>
                        <Button icon={RefreshCw} iconOnly size="sm" variant="ghost" aria-label="Đổi câu" tooltip="Đổi câu khác" onClick={() => void swap(k, j)} />
                      </li>
                    ))}
                  </ol>
                </section>
              ))}
            </div>
          )}
        </>
      )}
    </ModalForm>
  );
}
