import { Checkbox, Form, Switch } from 'antd';
import { FileUpload, NumberInput, Select, TextArea, TextInput, type UploadItem } from '@/components/common';
import { Q_LEVELS, Q_SKILL_OPTIONS } from '@/features/academic/questions/constants';
import type { QLevel, QSkill } from '@/features/academic/questions/types';
import { getBranches } from '@/features/system/useBranches';
import { DOC_AUDIENCE_OPTIONS, DOC_CATEGORY_OPTIONS, DOC_FILE_TYPE_OPTIONS } from './constants';
import type { DocAudience, DocCategory, DocFileType } from './types';

export interface DocFormValues {
  files?: UploadItem[];
  url?: string;
  title: string;
  subtitle?: string;
  description?: string;
  category: DocCategory;
  fileType: DocFileType;
  pages?: number;
  duration?: number;
  levels: QLevel[];
  skills: QSkill[];
  tags?: string[];
  publisher?: string;
  year?: number;
  audience: DocAudience[];
  branchIds?: string[];
  watermark: boolean;
  allowDownload: boolean;
}

const KIND: Record<DocFileType, 'document' | 'audio' | 'video' | undefined> = { pdf: 'document', audio: 'audio', video: 'video', link: undefined };

/** Tải tài liệu lên thư viện / sửa thông tin */
export function DocFormFields({ editing }: { editing?: boolean }) {
  const form = Form.useFormInstance<DocFormValues>();
  const fileType = Form.useWatch('fileType', form);

  return (
    <>
      <Form.Item name="fileType" label="Dạng tài liệu" rules={[{ required: true }]}>
        <Select options={DOC_FILE_TYPE_OPTIONS} showSearch={false} allowClear={false} onChange={() => form.setFieldValue('files', [])} />
      </Form.Item>
      <Form.Item name="category" label="Nhóm" rules={[{ required: true }]}>
        <Select options={DOC_CATEGORY_OPTIONS} showSearch={false} allowClear={false} />
      </Form.Item>

      {fileType === 'link' ? (
        <Form.Item name="url" label="Đường dẫn" className="md:col-span-2" rules={[{ required: true, type: 'url', message: 'Nhập link hợp lệ' }]}>
          <TextInput placeholder="https://..." />
        </Form.Item>
      ) : (
        <Form.Item
          name="files"
          label={editing ? 'Đổi file' : 'File'}
          className="md:col-span-2"
          extra={editing ? 'Bỏ trống = giữ file hiện tại (lượt xem, thống kê giữ nguyên).' : 'PDF tối đa 100 MB; audio / video tối đa 500 MB.'}
          rules={editing ? [] : [{ required: true, type: 'array', min: 1, message: 'Chọn file tải lên' }]}
        >
          <FileUpload kind={KIND[fileType ?? 'pdf']} maxFiles={1} maxSizeMB={fileType === 'pdf' ? 100 : 500} />
        </Form.Item>
      )}

      <Form.Item name="title" label="Tên tài liệu" className="md:col-span-2" rules={[{ required: true, whitespace: true, message: 'Nhập tên' }]}>
        <TextInput maxLength={160} placeholder="VD: Đề thi JLPT N3 — Tháng 12/2025" />
      </Form.Item>
      <Form.Item name="subtitle" label="Tên tiếng Nhật / phụ đề">
        <TextInput maxLength={160} placeholder="日本語能力試験 N3" />
      </Form.Item>
      <Form.Item name="publisher" label="Nguồn / NXB">
        <TextInput maxLength={80} placeholder="VD: 3A Corporation, JP Center biên soạn" />
      </Form.Item>
      {fileType === 'pdf' ? (
        <Form.Item name="pages" label="Số trang">
          <NumberInput unit="trang" min={1} />
        </Form.Item>
      ) : fileType === 'audio' || fileType === 'video' ? (
        <Form.Item name="duration" label="Thời lượng">
          <NumberInput unit="phút" min={1} />
        </Form.Item>
      ) : (
        <div />
      )}
      <Form.Item name="year" label="Năm">
        <NumberInput min={1990} max={2100} />
      </Form.Item>

      <Form.Item name="levels" label="Cấp độ" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn cấp độ' }]}>
        <Checkbox.Group options={Q_LEVELS.map((l) => ({ value: l, label: l }))} />
      </Form.Item>
      <Form.Item name="skills" label="Kỹ năng">
        <Select mode="multiple" options={Q_SKILL_OPTIONS} placeholder="Chọn kỹ năng" />
      </Form.Item>
      <Form.Item name="tags" label="Thẻ" className="md:col-span-2">
        <Select mode="tags" placeholder="VD: Đề thật, Có giải, Shin Kanzen" />
      </Form.Item>
      <Form.Item name="description" label="Mô tả" className="md:col-span-2">
        <TextArea rows={2} maxLength={500} />
      </Form.Item>

      <Form.Item name="audience" label="Ai được xem" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn ít nhất 1 nhóm' }]}>
        <Checkbox.Group options={DOC_AUDIENCE_OPTIONS} />
      </Form.Item>
      <Form.Item name="branchIds" label="Chi nhánh">
        <Select mode="multiple" placeholder="Tất cả chi nhánh" options={getBranches(false).map((b) => ({ value: b.id, label: b.name }))} />
      </Form.Item>
      <Form.Item name="watermark" label="Watermark tên người xem" valuePropName="checked">
        <Switch />
      </Form.Item>
      <Form.Item name="allowDownload" label="Cho phép tải về / in" valuePropName="checked" extra="Chỉ bật với tài liệu công khai.">
        <Switch />
      </Form.Item>
    </>
  );
}
