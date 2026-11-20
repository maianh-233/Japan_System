import { useMemo, useState } from 'react';
import { Form } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { TriangleAlert } from 'lucide-react';
import { DateInput, ModalForm, TextArea, useConfirm, useNotify } from '@/components/common';
import { fmtDate } from '@/lib/date';
import { useClassStore } from '../classes/classStore';
import { bookingsByRoom, openClassesIn, roomKey } from './roomUsage';
import * as roomApi from './roomApi';
import type { Room, RoomStatus } from './types';

interface MaintenanceForm {
  reason: string;
  until?: Dayjs;
}

/**
 * Đổi trạng thái phòng (dùng chung cho bảng + drawer chi tiết):
 *  - Bảo trì: nhập lý do + ngày dự kiến xong, cảnh báo các buổi học rơi vào thời gian bảo trì
 *  - Ngừng sử dụng: CHẶN nếu còn lớp đang mở lấy phòng làm phòng mặc định
 *  - Dùng lại: đổi ngay
 */
export function useRoomStatus() {
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const [maintaining, setMaintaining] = useState<Room | null>(null);
  const [form] = Form.useForm<MaintenanceForm>();
  const until = Form.useWatch('until', form);

  // Buổi học sắp tới trong phòng đang chọn bảo trì, đến hết ngày dự kiến xong
  const affected = useMemo(() => {
    if (!maintaining) return [];
    const today = dayjs().format('YYYY-MM-DD');
    const end = until ? until.format('YYYY-MM-DD') : dayjs().add(14, 'day').format('YYYY-MM-DD');
    return (bookingsByRoom(classes, sessions).get(roomKey(maintaining.branchId, maintaining.name)) ?? []).filter(
      (b) => b.session.status === 'scheduled' && b.session.date >= today && b.session.date <= end,
    );
  }, [maintaining, until, classes, sessions]);

  const change = async (room: Room, status: RoomStatus) => {
    if (status === 'active') {
      try {
        await roomApi.setRoomStatus(room.id, 'active');
      } catch (e) {
        notify.error({ title: `Chưa dùng lại được ${room.name}`, description: (e as Error).message });
        return;
      }
      notify.toast.success(`${room.name} đã dùng lại để xếp lớp`);
      return;
    }
    if (status === 'maintenance') {
      form.resetFields();
      setMaintaining(room);
      return;
    }
    const using = openClassesIn(room, classes);
    if (using.length) {
      notify.warning({
        title: `Chưa ngừng sử dụng được ${room.name}`,
        description: `Còn ${using.length} lớp đang mở dùng phòng này (${using.map((c) => c.code).join(', ')}). Đổi phòng cho các lớp trước.`,
      });
      return;
    }
    const ok = await confirm({
      title: `Ngừng sử dụng ${room.name}?`,
      description: 'Phòng sẽ không xuất hiện khi xếp lớp / đổi phòng. Lịch sử sử dụng vẫn được giữ.',
      tone: 'warning',
      confirmAction: 'save',
      confirmText: 'Ngừng sử dụng',
      onConfirm: () => roomApi.setRoomStatus(room.id, 'inactive'),
    });
    if (!ok) return;
    notify.success({ title: 'Đã ngừng sử dụng phòng', description: room.name });
  };

  const dialog = (
    <>
      <ModalForm<MaintenanceForm>
        form={form}
        open={Boolean(maintaining)}
        onClose={() => setMaintaining(null)}
        title={`Chuyển ${maintaining?.name ?? ''} sang bảo trì`}
        description="Phòng tạm thời không xếp được lớp mới. Lớp đang dùng phòng vẫn giữ lịch — cần đổi phòng cho từng buổi bị ảnh hưởng."
        submitAction="save"
        submitText="Chuyển bảo trì"
        successMessage="Đã chuyển phòng sang bảo trì"
        onSubmit={async (v) => {
          if (maintaining) await roomApi.setRoomStatus(maintaining.id, 'maintenance', { reason: v.reason.trim(), until: v.until?.format('YYYY-MM-DD') });
        }}
      >
        <Form.Item name="reason" label="Lý do" rules={[{ required: true, whitespace: true, message: 'Nhập lý do bảo trì' }]}>
          <TextArea rows={2} maxLength={300} placeholder="VD: sửa điều hòa, thay máy chiếu" />
        </Form.Item>
        <Form.Item name="until" label="Dự kiến xong">
          <DateInput disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
        </Form.Item>
        {affected.length > 0 && (
          <div className="flex gap-2.5 rounded-lg border border-warning-100 bg-warning-50 px-3 py-2.5 text-[13px] text-warning-700">
            <TriangleAlert size={16} className="mt-0.5 shrink-0" aria-hidden />
            <div>
              <p className="font-medium">
                {affected.length} buổi học {until ? `đến ${fmtDate(until.toISOString())}` : 'trong 2 tuần tới'} đang xếp ở phòng này
              </p>
              <p className="mt-0.5">
                {affected
                  .slice(0, 4)
                  .map((b) => `${b.cls.code} ${dayjs(b.session.date).format('DD/MM')} ${b.session.startTime}`)
                  .join(' · ')}
                {affected.length > 4 && ` · +${affected.length - 4} buổi`}
              </p>
              <p className="mt-1 text-warning-700/90">Vào Lớp học → Buổi học để đổi phòng cho từng buổi.</p>
            </div>
          </div>
        )}
      </ModalForm>
      {confirmDialog}
    </>
  );

  return { change, dialog };
}
