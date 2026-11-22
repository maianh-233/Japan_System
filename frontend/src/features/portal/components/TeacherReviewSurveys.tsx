import { useEffect, useState } from 'react';
import { Checkbox, Form, Rate } from 'antd';
import { Star } from 'lucide-react';
import { Button, ModalForm, TextArea } from '@/components/common';
import * as learningApi from '@/features/academic/classes/learningApi';
import type { ReviewCriterion } from '@/features/academic/classes/types';

const CRITERIA: Record<ReviewCriterion, string> = {
  method: 'Phương pháp giảng dạy',
  clarity: 'Giảng dễ hiểu',
  enthusiasm: 'Nhiệt tình, hỗ trợ',
  punctuality: 'Đúng giờ',
};

interface Values extends Record<ReviewCriterion, number> {
  overall: number;
  comment?: string;
  anonymous: boolean;
}

/**
 * Cổng HV — khảo sát đánh giá GV (ND-08): mở giữa khóa (từ ngày thi giữa kỳ) và quanh cuối khóa. Mỗi GV × đợt 1 lần, ẩn danh được.
 * Không có đợt nào đang mở / đã đánh giá hết → không hiện.
 */
export function TeacherReviewSurveys() {
  const [surveys, setSurveys] = useState<learningApi.ReviewSurvey[]>([]);
  const [target, setTarget] = useState<{ classId: string; classCode: string; teacherId: string; name: string; period: 'mid' | 'final' } | null>(null);

  const load = () => void learningApi.loadSurveys().then(setSurveys).catch(() => undefined);
  useEffect(load, []);

  const pending = surveys.flatMap((s) =>
    s.period ? s.teachers.filter((t) => !t.reviewed).map((t) => ({ classId: s.classId, classCode: s.classCode, teacherId: t.teacherId, name: t.name, period: s.period! })) : [],
  );
  if (!pending.length) return null;

  return (
    <section className="mb-6 rounded-[var(--radius-card)] border border-warning-100 bg-warning-50/40 p-4">
      <p className="mb-1 flex items-center gap-2 font-semibold text-ink">
        <Star size={18} className="text-warning-600" aria-hidden /> Khảo sát đánh giá giáo viên
      </p>
      <p className="mb-3 text-[13px] text-ink-secondary">Ý kiến của bạn giúp trung tâm nâng cao chất lượng giảng dạy. Có thể gửi ẩn danh.</p>
      <div className="flex flex-wrap gap-2">
        {pending.map((p) => (
          <Button key={`${p.classId}-${p.teacherId}`} icon={Star} size="sm" variant="soft" onClick={() => setTarget(p)}>
            {p.name} · {p.classCode} ({p.period === 'mid' ? 'giữa khóa' : 'cuối khóa'})
          </Button>
        ))}
      </div>

      <ModalForm<Values>
        key={target ? `${target.classId}-${target.teacherId}` : 'review'}
        open={Boolean(target)}
        onClose={() => setTarget(null)}
        title={`Đánh giá ${target?.name ?? ''}`}
        description={`${target?.classCode ?? ''} · đợt ${target?.period === 'mid' ? 'giữa khóa' : 'cuối khóa'}`}
        submitAction="send"
        submitText="Gửi đánh giá"
        successMessage="Cảm ơn bạn đã đánh giá!"
        initialValues={{ overall: 5, method: 5, clarity: 5, enthusiasm: 5, punctuality: 5, anonymous: true }}
        onSubmit={async (v) => {
          await learningApi.submitTeacherReview({
            classId: target!.classId,
            teacherId: target!.teacherId,
            overall: v.overall,
            ratings: { method: v.method, clarity: v.clarity, enthusiasm: v.enthusiasm, punctuality: v.punctuality },
            comment: v.comment?.trim() || undefined,
            anonymous: v.anonymous,
          });
          load();
        }}
      >
        <Form.Item name="overall" label="Đánh giá chung" rules={[{ required: true, type: 'number', min: 1, message: 'Chọn số sao' }]}>
          <Rate />
        </Form.Item>
        {(Object.keys(CRITERIA) as ReviewCriterion[]).map((k) => (
          <Form.Item key={k} name={k} label={CRITERIA[k]} rules={[{ required: true, type: 'number', min: 1, message: 'Chọn số sao' }]} className="!mb-2">
            <Rate />
          </Form.Item>
        ))}
        <Form.Item name="comment" label="Góp ý">
          <TextArea rows={3} maxLength={2000} />
        </Form.Item>
        <Form.Item name="anonymous" valuePropName="checked" className="!mb-0">
          <Checkbox>Ẩn danh (giáo viên không thấy tên bạn)</Checkbox>
        </Form.Item>
      </ModalForm>
    </section>
  );
}
