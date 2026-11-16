import { useMemo, useState } from 'react';
import { Checkbox, Form, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { TriangleAlert } from 'lucide-react';
import { DateInput, ModalForm, NumberInput, Select, TextArea, TextInput, useConfirm, useNotify } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { useClassStore } from '../classes/classStore';
import { EQUIPMENT, ISSUE_SEVERITY, isOpenIssue } from './constants';
import { bookingsByRoom, roomKey } from './roomUsage';
import { useRoomStore } from './roomStore';
import type { Equipment, IssueSeverity, RoomIssue } from './types';

interface ReportForm {
  roomId: string;
  equipment?: Equipment | 'other';
  title: string;
  severity: IssueSeverity;
  description?: string;
  toMaintenance?: boolean;
}
interface StartForm {
  assignee: string;
  dueDate?: Dayjs;
}
interface ResolveForm {
  resolution: string;
  cost?: number;
  reactivate?: boolean;
}

type Dialog = { kind: 'report'; roomId?: string } | { kind: 'start'; issue: RoomIssue } | { kind: 'resolve'; issue: RoomIssue } | null;

/** Lỗi gặp thường xuyên — bấm để điền nhanh tiêu đề */
const QUICK_TITLES: Partial<Record<Equipment | 'other', string[]>> = {
  projector: ['Máy chiếu không lên hình', 'Máy chiếu bị nhòe / lệch màu', 'Không nhận cáp HDMI'],
  aircon: ['Điều hòa không mát', 'Điều hòa chảy nước', 'Điều hòa kêu to'],
  speaker: ['Loa rè / không có tiếng'],
  tv: ['TV không lên nguồn', 'TV không nhận laptop'],
  whiteboard: ['Bảng bị mờ, khó xóa', 'Hết bút lông bảng'],
  other: ['Đèn hỏng / nhấp nháy', 'Bàn ghế gãy', 'Cửa / khóa hỏng', 'Wifi yếu / mất mạng', 'Thấm dột'],
};

/**
 * Báo hư hỏng & xử lý (dùng chung cho trang Phòng học + drawer chi tiết):
 *  báo hỏng → nhận xử lý (giao người sửa, hẹn ngày) → đã sửa xong (ghi cách sửa, chi phí).
 *  Hỏng nghiêm trọng → tùy chọn chuyển phòng sang Bảo trì luôn; sửa xong hết → tùy chọn dùng lại phòng.
 */
export function useRoomIssues() {
  const { user, branchId } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const rooms = useRoomStore((s) => s.rooms);
  const issues = useRoomStore((s) => s.issues);
  const reportIssue = useRoomStore((s) => s.reportIssue);
  const startIssue = useRoomStore((s) => s.startIssue);
  const resolveIssue = useRoomStore((s) => s.resolveIssue);
  const reopenIssue = useRoomStore((s) => s.reopenIssue);
  const setStatus = useRoomStore((s) => s.setStatus);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [reportForm] = Form.useForm<ReportForm>();

  const roomId = Form.useWatch('roomId', reportForm);
  const equipment = Form.useWatch('equipment', reportForm);
  const severity = Form.useWatch('severity', reportForm);
  const room = rooms.find((r) => r.id === roomId);

  // Buổi học 3 ngày tới trong phòng → nhắc đổi phòng khi hỏng nặng
  const upcoming = useMemo(() => {
    if (!room) return [];
    const today = dayjs().format('YYYY-MM-DD');
    const end = dayjs().add(3, 'day').format('YYYY-MM-DD');
    return (bookingsByRoom(classes, sessions).get(roomKey(room.branchId, room.name)) ?? []).filter(
      (b) => b.session.status === 'scheduled' && b.session.date >= today && b.session.date <= end,
    );
  }, [room, classes, sessions]);

  const roomName = (id: string) => rooms.find((r) => r.id === id)?.name ?? id;
  const reporter = user ? `${user.fullName} (${user.roleName})` : 'Nhân viên';

  const report = (presetRoomId?: string) => {
    reportForm.resetFields();
    setDialog({ kind: 'report', roomId: presetRoomId });
  };
  const start = (issue: RoomIssue) => setDialog({ kind: 'start', issue });
  const resolve = (issue: RoomIssue) => setDialog({ kind: 'resolve', issue });
  const reopen = async (issue: RoomIssue) => {
    const ok = await confirm({
      title: `Mở lại báo hỏng "${issue.title}"?`,
      description: 'Dùng khi đã báo sửa xong nhưng thiết bị vẫn còn lỗi.',
      tone: 'warning',
      confirmAction: 'save',
      confirmText: 'Mở lại',
    });
    if (!ok) return;
    reopenIssue(issue.id);
    notify.toast.info('Đã mở lại báo hỏng');
  };

  // Lựa chọn phòng: theo chi nhánh đang xem, bỏ phòng ngừng dùng
  const roomOptions = rooms
    .filter((r) => r.status !== 'inactive' && (branchId === 'all' || r.branchId === branchId))
    .sort((a, b) => a.name.localeCompare(b.name, 'vi', { numeric: true }))
    .map((r) => ({ value: r.id, label: r.name }));
  const equipmentOptions = [
    ...(room?.equipment ?? []).map((e) => ({ value: e, label: EQUIPMENT[e].label })),
    { value: 'other', label: 'Cơ sở vật chất khác (đèn, bàn ghế, cửa, wifi...)' },
  ];
  const quick = QUICK_TITLES[(equipment ?? 'other') as Equipment | 'other'] ?? [];

  const current = dialog?.kind === 'start' || dialog?.kind === 'resolve' ? dialog.issue : undefined;
  const currentRoom = current && rooms.find((r) => r.id === current.roomId);
  // Còn báo hỏng nặng khác chưa xong trong phòng này?
  const otherSevere = current ? issues.some((i) => i.id !== current.id && i.roomId === current.roomId && i.severity === 'high' && isOpenIssue(i)) : false;

  const dialogs = (
    <>
      {/* ---- Báo hỏng ---- */}
      <ModalForm<ReportForm>
        key={dialog?.kind === 'report' ? `report-${dialog.roomId ?? ''}` : 'report'}
        form={reportForm}
        open={dialog?.kind === 'report'}
        onClose={() => setDialog(null)}
        width={600}
        title="Báo hư hỏng"
        description="Bộ phận cơ sở vật chất / giáo vụ chi nhánh sẽ nhận được thông báo để sửa."
        submitAction="send"
        submitText="Gửi báo hỏng"
        successMessage="Đã gửi báo hỏng"
        initialValues={{ roomId: dialog?.kind === 'report' ? dialog.roomId : undefined, severity: 'medium', toMaintenance: true }}
        onSubmit={async (v) => {
          await new Promise((r) => setTimeout(r, 400));
          reportIssue({
            roomId: v.roomId,
            equipment: v.equipment && v.equipment !== 'other' ? v.equipment : undefined,
            title: v.title.trim(),
            description: v.description?.trim() || undefined,
            severity: v.severity,
            reportedBy: reporter,
          });
          const r = rooms.find((x) => x.id === v.roomId);
          if (v.severity === 'high' && v.toMaintenance && r?.status === 'active') {
            setStatus(r.id, 'maintenance', { reason: v.title.trim() });
            notify.warning({ title: `${r.name} đã chuyển sang bảo trì`, description: 'Nhớ đổi phòng cho các buổi học sắp tới.' });
          }
        }}
      >
        <Form.Item name="roomId" label="Phòng" rules={[{ required: true, message: 'Chọn phòng' }]}>
          <Select options={roomOptions} placeholder="Chọn phòng" onChange={() => reportForm.setFieldValue('equipment', undefined)} />
        </Form.Item>
        <Form.Item name="equipment" label="Hỏng gì" rules={[{ required: true, message: 'Chọn thiết bị' }]}>
          <Select options={equipmentOptions} placeholder={room ? 'Chọn thiết bị' : 'Chọn phòng trước'} disabled={!room} showSearch={false} />
        </Form.Item>
        <Form.Item name="title" label="Mô tả ngắn" rules={[{ required: true, whitespace: true, message: 'Nhập mô tả ngắn' }]} className="!mb-2">
          <TextInput placeholder="VD: Máy chiếu không lên hình" maxLength={120} />
        </Form.Item>
        {quick.length > 0 && (
          <div className="mb-4 flex flex-wrap gap-1.5">
            {quick.map((t) => (
              <button
                key={t}
                type="button"
                onClick={() => reportForm.setFieldValue('title', t)}
                className="rounded-full border border-line bg-page px-2.5 py-0.5 text-xs text-ink-secondary hover:border-primary-300 hover:bg-primary-50 hover:text-primary-700"
              >
                {t}
              </button>
            ))}
          </div>
        )}
        <Form.Item name="severity" label="Mức độ" rules={[{ required: true }]}>
          <Radio.Group className="flex flex-col gap-1.5">
            {(Object.keys(ISSUE_SEVERITY) as IssueSeverity[]).map((k) => (
              <Radio key={k} value={k}>
                <span className="font-medium">{ISSUE_SEVERITY[k].label}</span> <span className="text-[13px] text-ink-muted">— {ISSUE_SEVERITY[k].hint}</span>
              </Radio>
            ))}
          </Radio.Group>
        </Form.Item>
        <Form.Item name="description" label="Chi tiết">
          <TextArea rows={3} maxLength={500} placeholder="Hỏng từ khi nào, vị trí, đã thử khắc phục gì..." />
        </Form.Item>
        {severity === 'high' && room && (
          <div className="flex flex-col gap-2 rounded-lg border border-danger-100 bg-danger-50 px-3 py-2.5 text-[13px] text-danger-700">
            <p className="flex items-start gap-2">
              <TriangleAlert size={16} className="mt-0.5 shrink-0" aria-hidden />
              {upcoming.length
                ? `${room.name} có ${upcoming.length} buổi học trong 3 ngày tới (${upcoming
                    .slice(0, 3)
                    .map((b) => `${b.cls.code} ${dayjs(b.session.date).format('DD/MM')}`)
                    .join(', ')}${upcoming.length > 3 ? '...' : ''}) — cần đổi phòng nếu chưa sửa kịp.`
                : `${room.name} không có buổi học nào trong 3 ngày tới.`}
            </p>
            {room.status === 'active' && (
              <Form.Item name="toMaintenance" valuePropName="checked" className="!mb-0">
                <Checkbox>Chuyển phòng sang Bảo trì (không xếp lớp mới vào)</Checkbox>
              </Form.Item>
            )}
          </div>
        )}
      </ModalForm>

      {/* ---- Nhận xử lý ---- */}
      <ModalForm<StartForm>
        key={dialog?.kind === 'start' ? `start-${dialog.issue.id}` : 'start'}
        open={dialog?.kind === 'start'}
        onClose={() => setDialog(null)}
        title="Nhận xử lý"
        description={current && `${roomName(current.roomId)} — ${current.title}`}
        submitAction="save"
        submitText="Nhận xử lý"
        successMessage="Đã chuyển sang Đang sửa"
        initialValues={{ assignee: current?.assignee }}
        onSubmit={(v) => {
          if (current) startIssue(current.id, { assignee: v.assignee.trim(), dueDate: v.dueDate?.format('YYYY-MM-DD') });
        }}
      >
        <Form.Item name="assignee" label="Người / đơn vị sửa" rules={[{ required: true, whitespace: true, message: 'Nhập người sửa' }]}>
          <TextInput placeholder="VD: IT nội bộ, Điện lạnh Hòa Phát (SĐT)..." maxLength={120} />
        </Form.Item>
        <Form.Item name="dueDate" label="Hẹn sửa xong">
          <DateInput disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
        </Form.Item>
      </ModalForm>

      {/* ---- Sửa xong ---- */}
      <ModalForm<ResolveForm>
        key={dialog?.kind === 'resolve' ? `resolve-${dialog.issue.id}` : 'resolve'}
        open={dialog?.kind === 'resolve'}
        onClose={() => setDialog(null)}
        title="Xác nhận đã sửa xong"
        description={current && `${roomName(current.roomId)} — ${current.title}`}
        submitAction="save"
        submitText="Đã sửa xong"
        successMessage="Đã đóng báo hỏng"
        initialValues={{ reactivate: true }}
        onSubmit={(v) => {
          if (!current) return;
          resolveIssue(current.id, { resolution: v.resolution.trim(), cost: v.cost || undefined });
          if (v.reactivate && currentRoom?.status === 'maintenance' && !otherSevere) {
            setStatus(currentRoom.id, 'active');
            notify.toast.success(`${currentRoom.name} đã dùng lại để xếp lớp`);
          }
        }}
      >
        <Form.Item name="resolution" label="Đã xử lý thế nào" rules={[{ required: true, whitespace: true, message: 'Nhập cách xử lý' }]}>
          <TextArea rows={3} maxLength={500} placeholder="VD: Vệ sinh ống thoát nước điều hòa, nạp gas" />
        </Form.Item>
        <Form.Item name="cost" label="Chi phí">
          <NumberInput currency min={0} placeholder="0" />
        </Form.Item>
        {currentRoom?.status === 'maintenance' && !otherSevere && (
          <Form.Item name="reactivate" valuePropName="checked">
            <Checkbox>Đưa {currentRoom.name} trở lại sử dụng (hết bảo trì)</Checkbox>
          </Form.Item>
        )}
      </ModalForm>
      {confirmDialog}
    </>
  );

  return { report, start, resolve, reopen, dialogs };
}
