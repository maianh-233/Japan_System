import { Form, Radio } from 'antd';
import { Hash, Info } from 'lucide-react';
import { NumberInput, Select, TextArea, TextInput } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { formatCurrency } from '@/lib/format';
import {
  CATEGORY_OPTIONS,
  COURSE_STATUS_OPTIONS,
  FORMATS,
  LEVEL_OPTIONS,
  LEVEL_ORDER,
  STUDY_MODES,
  TEACHER_TYPES,
} from './constants';
import { useCourseStore, type CourseInput } from './courseStore';
import type { CourseFormat, StudyMode, TeacherType } from './types';

export type CourseFormValues = CourseInput;

/** Nhóm nút chọn 1 trong n — hiện đủ lựa chọn, bấm 1 lần (thay cho dropdown khi ít lựa chọn) */
function Segmented<K extends string>({ options, value, onChange }: { options: Record<K, { label: string }>; value?: K; onChange?: (v: K) => void }) {
  return (
    <Radio.Group
      optionType="button"
      buttonStyle="solid"
      value={value}
      onChange={(e) => onChange?.(e.target.value)}
      className="flex w-full [&_.ant-radio-button-wrapper]:flex-1 [&_.ant-radio-button-wrapper]:text-center"
      options={(Object.keys(options) as K[]).map((k) => ({ value: k, label: options[k].label }))}
    />
  );
}

/** Ô nhập khóa học — dùng cho Thêm, Sửa, Nhân bản. `editingId` để bỏ qua chính nó khi kiểm tra trùng mã. */
export function CourseFormFields({ editingId }: { editingId?: string }) {
  const { branches } = useAuth();
  const isCodeTaken = useCourseStore((s) => s.isCodeTaken);

  return (
    <>
      <Form.Item name="name" label="Tên khóa học" rules={[{ required: true, whitespace: true, message: 'Nhập tên khóa học' }]}>
        <TextInput placeholder="VD: JLPT N3 cấp tốc" maxLength={120} />
      </Form.Item>
      <Form.Item
        name="code"
        label="Mã khóa"
        normalize={(v: string) => v?.toUpperCase().replace(/\s+/g, '-')}
        rules={[
          { required: true, message: 'Nhập mã khóa' },
          { pattern: /^[A-Z0-9-]+$/, message: 'Chỉ gồm chữ không dấu, số và dấu gạch ngang' },
          { validator: (_, v: string) => (v && isCodeTaken(v, editingId) ? Promise.reject(new Error('Mã khóa đã tồn tại')) : Promise.resolve()) },
        ]}
      >
        <TextInput icon={Hash} placeholder="VD: N3-G-OFF" />
      </Form.Item>

      <Form.Item name="category" label="Loại khóa" rules={[{ required: true, message: 'Chọn loại khóa' }]}>
        <Select placeholder="Chọn loại" options={CATEGORY_OPTIONS} showSearch={false} />
      </Form.Item>
      <div className="grid grid-cols-2 gap-x-3">
        <Form.Item name="levelFrom" label="Đầu vào" rules={[{ required: true, message: 'Chọn' }]}>
          <Select options={LEVEL_OPTIONS} showSearch={false} allowClear={false} />
        </Form.Item>
        <Form.Item
          name="levelTo"
          label="Đầu ra"
          dependencies={['levelFrom']}
          rules={[
            { required: true, message: 'Chọn' },
            ({ getFieldValue }) => ({
              validator: (_, v) =>
                v && LEVEL_ORDER.indexOf(v) < LEVEL_ORDER.indexOf(getFieldValue('levelFrom'))
                  ? Promise.reject(new Error('Đầu ra phải ≥ đầu vào'))
                  : Promise.resolve(),
            }),
          ]}
        >
          <Select options={LEVEL_OPTIONS} showSearch={false} allowClear={false} />
        </Form.Item>
      </div>

      {/* ===== 3 lựa chọn chính ===== */}
      <Form.Item name="format" label="Hình thức lớp" rules={[{ required: true }]} className="md:col-span-2">
        <Segmented<CourseFormat> options={FORMATS} />
      </Form.Item>
      <Form.Item name="teacherType" label="Giáo viên" rules={[{ required: true }]} className="md:col-span-2">
        <Segmented<TeacherType> options={TEACHER_TYPES} />
      </Form.Item>
      <Form.Item name="mode" label="Học tại" rules={[{ required: true }]} className="md:col-span-2">
        <Segmented<StudyMode> options={STUDY_MODES} />
      </Form.Item>

      {/* ===== Thời lượng ===== */}
      <Form.Item name="sessions" label="Tổng số buổi" rules={[{ required: true, message: 'Nhập số buổi' }]}>
        <NumberInput min={1} max={500} unit="buổi" />
      </Form.Item>
      <div className="grid grid-cols-2 gap-x-3">
        <Form.Item name="minutesPerSession" label="Mỗi buổi" rules={[{ required: true, message: 'Nhập' }]}>
          <NumberInput min={15} max={480} step={15} unit="phút" />
        </Form.Item>
        <Form.Item name="sessionsPerWeek" label="Buổi / tuần" rules={[{ required: true, message: 'Nhập' }]}>
          <NumberInput min={1} max={7} unit="buổi" />
        </Form.Item>
      </div>

      {/* ===== Học phí + sĩ số: đổi theo hình thức ===== */}
      <Form.Item noStyle shouldUpdate={(a, b) => a.format !== b.format}>
        {({ getFieldValue }) => {
          const isPrivate = getFieldValue('format') === 'private';
          return (
            <>
              <Form.Item
                name="fee"
                label={isPrivate ? 'Học phí mỗi buổi' : 'Học phí trọn khóa'}
                extra={isPrivate ? 'Tổng học phí = số buổi × giá mỗi buổi' : 'Giá cho 1 học viên, chưa gồm ưu đãi'}
                rules={[{ required: true, message: 'Nhập học phí' }]}
              >
                <NumberInput currency min={0} step={50_000} />
              </Form.Item>
              {isPrivate ? (
                <div className="mb-6 flex items-center gap-2 self-end rounded-lg bg-page px-3 py-2.5 text-[13px] text-ink-secondary">
                  <Info size={16} className="shrink-0 text-ink-muted" aria-hidden />1 học viên / 1 giáo viên — không cần sĩ số
                </div>
              ) : (
                <Form.Item name="maxStudents" label="Sĩ số tối đa" rules={[{ required: true, message: 'Nhập sĩ số' }]}>
                  <NumberInput min={2} max={60} unit="học viên" />
                </Form.Item>
              )}
            </>
          );
        }}
      </Form.Item>

      <Form.Item noStyle shouldUpdate>
        {({ getFieldValue }) => {
          const sessions = getFieldValue('sessions') as number | undefined;
          const minutes = getFieldValue('minutesPerSession') as number | undefined;
          const perWeek = getFieldValue('sessionsPerWeek') as number | undefined;
          const fee = getFieldValue('fee') as number | undefined;
          if (!sessions || !minutes) return null;
          const weeks = perWeek ? Math.ceil(sessions / perWeek) : undefined;
          const total = getFieldValue('format') === 'private' && fee ? fee * sessions : undefined;
          return (
            <p className="mb-5 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-[13px] text-primary-800 md:col-span-2">
              Tổng <strong>{Math.round((sessions * minutes) / 60)} giờ học</strong>
              {weeks && <> · khoảng <strong>{weeks} tuần</strong> (~{Math.ceil(weeks / 4.3)} tháng)</>}
              {total !== undefined && <> · trọn khóa <strong>{formatCurrency(total)}</strong></>}
            </p>
          );
        }}
      </Form.Item>

      <Form.Item name="branchIds" label="Chi nhánh mở bán" rules={[{ required: true, message: 'Chọn ít nhất 1 chi nhánh' }]}>
        <Select mode="multiple" placeholder="Chọn chi nhánh" options={branches.map((b) => ({ value: b.id, label: b.name }))} />
      </Form.Item>
      <Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}>
        <Select options={COURSE_STATUS_OPTIONS} showSearch={false} allowClear={false} />
      </Form.Item>

      <Form.Item name="description" label="Mô tả / nội dung khóa" className="md:col-span-2">
        <TextArea maxLength={1000} placeholder="Giáo trình, mục tiêu đầu ra, đối tượng phù hợp..." />
      </Form.Item>
    </>
  );
}
