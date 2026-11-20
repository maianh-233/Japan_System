import { Form, Radio, Rate } from 'antd';
import { ModalForm, TextArea } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { fmtDateTime } from '@/lib/date';
import { useClassStore } from '../classStore';
import { EVAL_RECOMMENDATION, EVAL_SKILLS } from '../constants';
import type { EvalRecommendation, EvalSkill, StudentEvaluation } from '../types';
import { USE_API } from '@/lib/api/config';
import * as portalApi from '@/features/portal/portalApi';

type Values = Pick<StudentEvaluation, 'skills' | 'attitude' | 'strengths' | 'weaknesses' | 'recommendation' | 'advice'>;

interface Props {
  open: boolean;
  onClose: () => void;
  classId: string;
  studentId: string;
  studentName: string;
  teacherId: string;
}

const RATE_TIPS = ['Yếu', 'Trung bình', 'Khá', 'Tốt', 'Xuất sắc'];

/** GV viết / sửa nhận xét cuối khóa cho 1 học viên */
export function EvaluationModal({ open, onClose, classId, studentId, studentName, teacherId }: Props) {
  const { user } = useAuth();
  const saveEvaluation = useClassStore((s) => s.saveEvaluation);
  const existing = useClassStore((s) => s.evaluations.find((e) => e.classId === classId && e.studentId === studentId));

  return (
    <ModalForm<Values>
      key={`${studentId}-${open}`}
      open={open}
      onClose={onClose}
      width={680}
      title={`Nhận xét cuối khóa · ${studentName}`}
      description={
        existing
          ? `Viết ${fmtDateTime(existing.createdAt)} · sửa lần cuối ${fmtDateTime(existing.updatedAt)}. Sale và giáo vụ xem bản mới nhất.`
          : 'Đánh giá tổng thể cả khóa — Sale dùng để tư vấn học viên học khóa tiếp theo. Học viên không thấy nhận xét này.'
      }
      submitAction="save"
      submitText={existing ? 'Lưu thay đổi' : 'Gửi nhận xét'}
      successMessage="Đã lưu nhận xét cuối khóa"
      initialValues={
        existing ?? {
          skills: { vocab: 3, grammar: 3, reading: 3, listening: 3, speaking: 3 },
          attitude: 3,
          recommendation: 'next_level',
        }
      }
      onSubmit={async (v) => {
        if (USE_API) return void (await portalApi.saveEvaluation({ ...v, classId, studentId }));
        saveEvaluation({ ...v, classId, studentId, teacherId, author: user?.fullName ?? '' });
      }}
    >
      <div className="mb-4 rounded-[var(--radius-card)] border border-line px-4 py-3">
        <p className="mb-1 text-[13px] font-medium text-ink">Năng lực theo kỹ năng</p>
        <div className="grid gap-x-6 sm:grid-cols-2">
          {(Object.keys(EVAL_SKILLS) as EvalSkill[]).map((k) => (
            <Form.Item key={k} name={['skills', k]} label={EVAL_SKILLS[k]} className="!mb-1" layout="horizontal" labelCol={{ flex: '150px' }} labelAlign="left" colon={false}>
              <Rate tooltips={RATE_TIPS} />
            </Form.Item>
          ))}
          <Form.Item name="attitude" label="Thái độ, chuyên cần" className="!mb-1" layout="horizontal" labelCol={{ flex: '150px' }} labelAlign="left" colon={false}>
            <Rate tooltips={RATE_TIPS} />
          </Form.Item>
        </div>
      </div>

      <Form.Item name="strengths" label="Điểm mạnh">
        <TextArea rows={2} maxLength={500} placeholder="Vd: nắm ngữ pháp chắc, chăm làm bài tập" />
      </Form.Item>
      <Form.Item name="weaknesses" label="Cần cải thiện">
        <TextArea rows={2} maxLength={500} placeholder="Vd: nghe hiểu tốc độ thường còn chậm" />
      </Form.Item>
      <Form.Item name="recommendation" label="Đề xuất bước tiếp theo" rules={[{ required: true }]}>
        <Radio.Group className="grid gap-1.5 sm:grid-cols-2">
          {(Object.keys(EVAL_RECOMMENDATION) as EvalRecommendation[]).map((k) => (
            <Radio key={k} value={k}>
              <span className="font-medium">{EVAL_RECOMMENDATION[k].label}</span>
              <span className="block text-xs text-ink-muted">{EVAL_RECOMMENDATION[k].hint}</span>
            </Radio>
          ))}
        </Radio.Group>
      </Form.Item>
      <Form.Item name="advice" label="Ghi chú cho trung tâm / Sale" extra="Lớp, giáo viên, lịch học phù hợp; điều cần lưu ý khi tư vấn...">
        <TextArea rows={2} maxLength={500} />
      </Form.Item>
    </ModalForm>
  );
}
