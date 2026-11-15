import { Form, Radio } from 'antd';
import { BookMarked, Hash } from 'lucide-react';
import { Select, TextArea, TextInput } from '@/components/common';
import { LEVEL_OPTIONS } from '../courses/constants';
import { TAG_SUGGESTIONS, TIERS } from './constants';
import type { CurriculumInput } from './curriculumStore';
import type { CurriculumTier } from './types';

export type CurriculumFormValues = Omit<CurriculumInput, 'author'>;

/** Ô nhập thông tin chung của lộ trình — dùng cho Tạo mới và tab Thông tin */
export function CurriculumFormFields({ hideStatus }: { hideStatus?: boolean }) {
  return (
    <>
      <Form.Item name="name" label="Tên lộ trình" rules={[{ required: true, whitespace: true, message: 'Nhập tên lộ trình' }]} className="md:col-span-2">
        <TextInput placeholder="VD: Lộ trình N3 — Shin Kanzen + Mimikara" maxLength={120} />
      </Form.Item>
      <Form.Item
        name="code"
        label="Mã lộ trình"
        normalize={(v: string) => v?.toUpperCase().replace(/\s+/g, '-')}
        extra="Các phiên bản của cùng lộ trình dùng chung mã"
        rules={[
          { required: true, message: 'Nhập mã' },
          { pattern: /^[A-Z0-9-]+$/, message: 'Chỉ gồm chữ không dấu, số và dấu gạch ngang' },
        ]}
      >
        <TextInput icon={Hash} placeholder="VD: LT-N3" />
      </Form.Item>
      <div className="grid grid-cols-2 gap-x-3">
        <Form.Item name="version" label="Phiên bản" rules={[{ required: true, message: 'Nhập' }, { pattern: /^v\d+\.\d+$/, message: 'Dạng v1.0' }]}>
          <TextInput placeholder="v1.0" />
        </Form.Item>
        <Form.Item name="level" label="Cấp độ" rules={[{ required: true, message: 'Chọn' }]}>
          <Select options={LEVEL_OPTIONS} showSearch={false} allowClear={false} />
        </Form.Item>
      </div>

      <Form.Item name="tier" label="Bậc lộ trình" rules={[{ required: true }]} className="md:col-span-2">
        <Radio.Group className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          {(Object.keys(TIERS) as CurriculumTier[]).map((k) => {
            const t = TIERS[k];
            const Icon = t.icon;
            return (
              <Radio key={k} value={k} className="!mr-0 rounded-lg border border-line p-3 [&.ant-radio-wrapper-checked]:border-primary-500 [&.ant-radio-wrapper-checked]:bg-primary-50">
                <span className="flex flex-col gap-0.5">
                  <span className="inline-flex items-center gap-1.5 font-medium text-ink">
                    <Icon size={15} aria-hidden /> {t.label}
                  </span>
                  <span className="text-xs text-ink-muted">{t.hint}</span>
                </span>
              </Radio>
            );
          })}
        </Radio.Group>
      </Form.Item>
      <Form.Item noStyle shouldUpdate={(a, b) => a.tier !== b.tier}>
        {({ getFieldValue }) =>
          getFieldValue('tier') === 'advanced' ? (
            <Form.Item
              name="entryRequirement"
              label="Điều kiện áp dụng"
              rules={[{ required: true, whitespace: true, message: 'Nêu điều kiện đầu vào để Giáo vụ chọn đúng lộ trình khi mở lớp' }]}
              className="md:col-span-2"
            >
              <TextInput placeholder="VD: Đã đỗ N4 hoặc điểm test đầu vào TB lớp ≥ 85%" />
            </Form.Item>
          ) : null
        }
      </Form.Item>

      <Form.Item name="textbook" label="Giáo trình">
        <TextInput icon={BookMarked} placeholder="VD: Shin Kanzen Master N3" />
      </Form.Item>
      <Form.Item name="tags" label="Tag">
        <Select mode="tags" placeholder="Chọn hoặc gõ tag mới rồi Enter" options={TAG_SUGGESTIONS.map((t) => ({ value: t, label: t }))} />
      </Form.Item>
      {!hideStatus && <Form.Item name="status" hidden />}
      <Form.Item name="description" label="Mô tả" className="md:col-span-2">
        <TextArea maxLength={1000} placeholder="Mục tiêu, điểm khác so với bản chuẩn, lưu ý cho giáo viên..." />
      </Form.Item>
    </>
  );
}
