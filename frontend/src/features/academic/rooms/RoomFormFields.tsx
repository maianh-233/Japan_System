import { Checkbox, Form } from 'antd';
import { DoorOpen } from 'lucide-react';
import { NumberInput, Select, TextArea, TextInput } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { EQUIPMENT, ROOM_TYPE_OPTIONS } from './constants';
import { useRoomStore, type RoomInput } from './roomStore';
import type { Equipment } from './types';

export type RoomFormValues = Pick<RoomInput, 'branchId' | 'name' | 'floor' | 'type' | 'capacity' | 'area' | 'equipment' | 'note'>;

/**
 * Ô nhập phòng học — dùng cho Thêm và Sửa.
 * `editingId`: bỏ qua chính nó khi kiểm tra trùng tên. `lockBranch`: phòng đã có lớp thì không chuyển chi nhánh.
 */
export function RoomFormFields({ editingId, lockBranch, minCapacity }: { editingId?: string; lockBranch?: boolean; minCapacity?: { value: number; classCode: string } }) {
  const { branches } = useAuth();
  const isNameTaken = useRoomStore((s) => s.isNameTaken);

  return (
    <>
      <Form.Item
        name="name"
        label="Tên phòng"
        dependencies={['branchId']}
        rules={[
          { required: true, whitespace: true, message: 'Nhập tên phòng' },
          ({ getFieldValue }) => ({
            validator: (_, v: string) =>
              v && isNameTaken(getFieldValue('branchId'), v, editingId) ? Promise.reject(new Error('Chi nhánh đã có phòng trùng tên')) : Promise.resolve(),
          }),
        ]}
        extra={editingId ? 'Đổi tên sẽ cập nhật theo ở các lớp và buổi học đang dùng phòng.' : undefined}
      >
        <TextInput icon={DoorOpen} placeholder="VD: P.301" maxLength={40} />
      </Form.Item>
      <Form.Item
        name="branchId"
        label="Chi nhánh"
        rules={[{ required: true, message: 'Chọn chi nhánh' }]}
        extra={lockBranch ? 'Phòng đang có lớp — không chuyển chi nhánh được.' : undefined}
      >
        <Select disabled={lockBranch} options={branches.map((b) => ({ value: b.id, label: b.name }))} showSearch={false} allowClear={false} />
      </Form.Item>

      <Form.Item name="type" label="Loại phòng" rules={[{ required: true, message: 'Chọn loại phòng' }]}>
        <Select options={ROOM_TYPE_OPTIONS} showSearch={false} allowClear={false} />
      </Form.Item>
      <Form.Item name="floor" label="Tầng / khu">
        <TextInput placeholder="VD: Tầng 3" maxLength={40} />
      </Form.Item>

      <Form.Item
        name="capacity"
        label="Sức chứa"
        rules={[
          { required: true, message: 'Nhập sức chứa' },
          { type: 'number', min: 1, max: 200, message: 'Từ 1 đến 200 chỗ' },
        ]}
        extra={minCapacity ? `Lớp ${minCapacity.classCode} đang dùng phòng có sĩ số tối đa ${minCapacity.value}.` : undefined}
      >
        <NumberInput unit="chỗ" min={1} max={200} />
      </Form.Item>
      <Form.Item name="area" label="Diện tích">
        <NumberInput unit="m²" min={1} max={1000} />
      </Form.Item>

      <Form.Item name="equipment" label="Thiết bị" className="md:col-span-2">
        <Checkbox.Group className="grid w-full grid-cols-1 gap-2 sm:grid-cols-2">
          {(Object.keys(EQUIPMENT) as Equipment[]).map((k) => {
            const Icon = EQUIPMENT[k].icon;
            return (
              <Checkbox key={k} value={k}>
                <span className="inline-flex items-center gap-1.5">
                  <Icon size={15} className="text-ink-muted" aria-hidden /> {EQUIPMENT[k].label}
                </span>
              </Checkbox>
            );
          })}
        </Checkbox.Group>
      </Form.Item>

      <Form.Item name="note" label="Ghi chú" className="md:col-span-2">
        <TextArea rows={3} maxLength={500} placeholder="VD: phòng thảm, ưu tiên lớp thiếu nhi" />
      </Form.Item>
    </>
  );
}
