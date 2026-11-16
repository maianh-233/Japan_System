import { useMemo } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { DateInput, ModalForm, Select, TextArea } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { notStarted, sessionTeachers } from '@/features/academic/classes/sessionUtils';
import { usePortalStore } from '../portalStore';
import { shortCode, useTeacherId } from '../usePortal';

interface Props {
  open: boolean;
  onClose: () => void;
  /** Mở sẵn cho 1 buổi (bấm từ thời khóa biểu) */
  sessionId?: string;
}

type Values = { sessionId: string; reasonType: string; reason: string; makeupAt?: dayjs.Dayjs };
const REASONS = ['Ốm / sức khỏe', 'Việc gia đình đột xuất', 'Sự cố thiết bị / mạng (lớp online)', 'Khác'];

/** GV xin hủy 1 buổi dạy — chỉ buổi CHƯA diễn ra; giáo vụ duyệt, mời GV dạy thay hoặc xếp học bù */
export function CancelRequestModal({ open, onClose, sessionId }: Props) {
  const me = useTeacherId();
  const sessions = useClassStore((s) => s.sessions);
  const classes = useClassStore((s) => s.classes);
  const requests = usePortalStore((s) => s.cancelRequests);
  const add = usePortalStore((s) => s.addCancelRequest);

  const options = useMemo(() => {
    const requested = new Set(requests.filter((r) => r.status !== 'rejected').map((r) => r.sessionId));
    return sessions
      .filter((s) => s.status === 'scheduled' && notStarted(s) && !requested.has(s.id) && dayjs(s.date).diff(dayjs(), 'day') <= 45)
      .map((s) => ({ s, cls: classes.find((c) => c.id === s.classId) }))
      .filter(({ s, cls }) => cls && sessionTeachers(s, cls).includes(me))
      .sort((a, b) => (a.s.date + a.s.startTime).localeCompare(b.s.date + b.s.startTime));
  }, [sessions, classes, requests, me]);

  return (
    <ModalForm<Values>
      key={sessionId ?? 'cancel'}
      open={open}
      onClose={onClose}
      title="Gửi đơn hủy buổi dạy"
      description="Gửi càng sớm càng tốt (tối thiểu 24 giờ trước buổi học, trừ trường hợp đột xuất) để giáo vụ kịp tìm GV dạy thay."
      submitAction="send"
      submitText="Gửi đơn"
      successMessage="Đã gửi đơn — giáo vụ sẽ xử lý và báo lại"
      initialValues={{ sessionId }}
      onSubmit={(v) => {
        const s = sessions.find((x) => x.id === v.sessionId)!;
        if (!notStarted(s)) throw new Error('Buổi đã bắt đầu — gọi giáo vụ để báo sự cố.');
        add({ sessionId: s.id, classId: s.classId, teacherId: me, reasonType: v.reasonType, reason: v.reason, makeupAt: v.makeupAt?.toISOString() });
      }}
    >
      <Form.Item name="sessionId" label="Buổi dạy" rules={[{ required: true, message: 'Chọn buổi' }]}>
        <Select
          placeholder="Chọn buổi"
          options={options.map(({ s, cls }) => ({ value: s.id, label: `${dayjs(s.date).format('dd DD/MM')} ${s.startTime} · ${shortCode(cls!)} · buổi ${s.no}${s.kind === 'exam' ? ' (THI)' : ''}` }))}
        />
      </Form.Item>
      <Form.Item noStyle shouldUpdate={(a, b) => a.sessionId !== b.sessionId}>
        {({ getFieldValue }) => {
          const s = sessions.find((x) => x.id === getFieldValue('sessionId'));
          const h = s ? dayjs(`${s.date}T${s.startTime}`).diff(dayjs(), 'hour') : 99;
          return h < 24 ? <p className="mb-3 rounded-lg bg-warning-50 px-3 py-2 text-[13px] text-warning-700">Buổi này diễn ra trong {Math.max(0, h)} giờ tới — nên gọi điện báo giáo vụ ngay sau khi gửi đơn.</p> : null;
        }}
      </Form.Item>
      <Form.Item name="reasonType" label="Lý do" rules={[{ required: true, message: 'Chọn lý do' }]}>
        <Select options={REASONS.map((x) => ({ value: x, label: x }))} showSearch={false} />
      </Form.Item>
      <Form.Item name="reason" label="Chi tiết" rules={[{ required: true, message: 'Nhập chi tiết' }, { min: 10, message: 'Tối thiểu 10 ký tự' }]}>
        <TextArea rows={2} maxLength={300} />
      </Form.Item>
      <Form.Item name="makeupAt" label="Đề xuất thời gian dạy bù" extra="Giáo vụ sẽ xếp phòng và báo lớp.">
        <DateInput showTime disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
      </Form.Item>
    </ModalForm>
  );
}
