import { useMemo, useState } from 'react';
import { Form, Modal, Radio } from 'antd';
import dayjs from 'dayjs';
import { ArrowLeftRight, CalendarX2, CalendarPlus } from 'lucide-react';
import { Button, DateInput, Select, TextArea, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { TEACHERS, teacherName } from '@/features/academic/classes/constants';
import { SessionScheduleModal } from '@/features/academic/classes/detail/SessionScheduleModal';
import { notStarted, sessionTeachers } from '@/features/academic/classes/sessionUtils';
import type { ClassInfo, ClassSession } from '@/features/academic/classes/types';
import { busyStaff } from '@/features/academic/sessions/sessionOps';
import { useHrStore } from '@/features/hr/hrStore';
import type { ShiftKey } from '@/features/hr/types';
import { usePortalStore } from '@/features/portal/portalStore';
import type { CancelRequest } from '@/features/portal/types';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDateTime } from '@/lib/date';

type Action = 'invite' | 'makeup' | 'cancel';

interface Props {
  /** Duyệt đơn GV xin hủy buổi — hoặc bỏ trống để chỉ MỜI DẠY THAY cho buổi (mời thêm khi chưa ai nhận) */
  request?: CancelRequest;
  session: ClassSession;
  cls: ClassInfo;
  onClose: () => void;
}

const shiftOf = (t: string): ShiftKey => (t < '12:00' ? 'morning' : t < '17:30' ? 'afternoon' : 'evening');

/**
 * Xử lý buổi GV không dạy được — MỘT luồng duy nhất cho dạy thay:
 *  1. Mời GV dạy thay (1 hoặc nhiều người, ai nhận trước thì dạy; nhận → tự tính lương dạy thay)
 *  2. Hủy buổi & xếp học bù ngay (dùng lại modal xếp buổi của trang Lớp học)
 *  3. Hủy buổi, xếp bù sau
 *  Đổi GV TRỰC TIẾP trong chi tiết buổi chỉ dùng khi khẩn cấp (sự cố giữa buổi, GV đã đồng ý qua điện thoại).
 */
export function HandleCancelModal({ request, session: s, cls, onClose }: Props) {
  const { user } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const sessions = useClassStore((st) => st.sessions);
  const classes = useClassStore((st) => st.classes);
  const updateSession = useClassStore((st) => st.updateSession);
  const employees = useHrStore((st) => st.employees);
  const invites = usePortalStore((st) => st.subInvites);
  const { decideCancelRequest, addSubInvites } = usePortalStore();
  const [action, setAction] = useState<Action>('invite');
  const [makeupFor, setMakeupFor] = useState<string | null>(null);
  const [form] = Form.useForm<{ teacherIds: string[]; respondBy?: dayjs.Dayjs; message?: string }>();
  const by = user?.fullName ?? 'Giáo vụ';
  const upcoming = notStarted(s);
  const absentIds = request ? [request.teacherId] : sessionTeachers(s, cls);

  const candidates = useMemo(() => {
    const busy = busyStaff(s.date, s.startTime, s.endTime, sessions, classes, s.id);
    const shift = shiftOf(s.startTime);
    const wd = ((dayjs(s.date).day() + 6) % 7) + 1;
    const asked = new Map(invites.filter((i) => i.sessionId === s.id).map((i) => [i.toTeacherId, i.status]));
    return TEACHERS.filter((t) => t.branchIds.includes(cls.branchId) && !absentIds.includes(t.id))
      .map((t) => {
        const avail = employees.find((e) => e.id === t.id)?.teacher?.availability?.[wd]?.includes(shift);
        const b = busy.get(t.id);
        const st = asked.get(t.id);
        return {
          value: t.id,
          disabled: Boolean(b) || st === 'pending' || st === 'declined',
          rank: (b ? 3 : 0) + (avail ? 0 : 1),
          label: `${t.name}${t.type === 'jp' ? ' (GV Nhật)' : ''} — ${b ? `đang dạy ${b}` : st === 'pending' ? 'đã mời, chờ trả lời' : st === 'declined' ? 'đã từ chối' : avail ? 'rảnh · có đăng ký ca này' : 'rảnh · chưa đăng ký ca này'}`,
        };
      })
      .sort((a, b) => a.rank - b.rank);
  }, [s, cls, sessions, classes, invites, employees, absentIds]);

  const cancelSession = (reason: string) =>
    updateSession(s.id, { status: 'cancelled', note: reason, cancelledBy: { name: by, at: new Date().toISOString() } });

  const submit = async () => {
    const reason = request ? `GV ${teacherName(request.teacherId)} xin nghỉ — ${request.reasonType}` : 'GV không dạy được';
    if (action === 'invite') {
      const v = await form.validateFields();
      addSubInvites(
        v.teacherIds.map((id) => ({
          sessionId: s.id,
          classId: cls.id,
          fromTeacherId: absentIds[0],
          toTeacherId: id,
          reason: request ? `${teacherName(request.teacherId)}: ${request.reasonType}` : (v.message ?? 'GV lớp vắng'),
          invitedBy: by,
          respondBy: (v.respondBy ?? dayjs(`${s.date}T${s.startTime}`).subtract(3, 'hour')).toISOString(),
        })),
      );
      updateSession(s.id, { note: `${reason} — đang mời ${v.teacherIds.map(teacherName).join(', ')} dạy thay` });
      if (request) decideCancelRequest(request.id, true, by, `Đã mời ${v.teacherIds.map(teacherName).join(' / ')} dạy thay`);
      notify.success({ title: 'Đã gửi lời mời dạy thay', description: 'GV nhận trên Cổng giáo viên. Theo dõi ở tab "Dạy thay".' });
      onClose();
      return;
    }
    cancelSession(reason);
    if (request) decideCancelRequest(request.id, true, by, action === 'makeup' ? 'Hủy buổi, đã xếp học bù' : 'Hủy buổi — giáo vụ sẽ xếp học bù sau');
    if (action === 'makeup') setMakeupFor(s.id);
    else {
      notify.success({ title: 'Đã hủy buổi', description: 'Học viên & phụ huynh nhận thông báo. Nhớ xếp học bù ở trang Lớp học.' });
      onClose();
    }
  };

  if (makeupFor) return <SessionScheduleModal cls={cls} mode={{ kind: 'create', origin: 'makeup', makeupForId: makeupFor }} onClose={onClose} />;

  const OPTIONS: { value: Action; label: string; hint: string; icon: typeof CalendarX2 }[] = [
    { value: 'invite', label: 'Mời GV dạy thay', hint: 'Buổi giữ nguyên lịch. Mời được nhiều GV — ai nhận trước thì dạy, lời mời còn lại tự hết hiệu lực.', icon: ArrowLeftRight },
    { value: 'makeup', label: 'Hủy buổi & xếp học bù ngay', hint: request?.makeupAt ? `GV đề xuất dạy bù: ${fmtDateTime(request.makeupAt)}` : 'Chọn ngày giờ, phòng, GV cho buổi bù.', icon: CalendarPlus },
    { value: 'cancel', label: 'Hủy buổi, xếp bù sau', hint: 'Buổi bị hủy hiện ở danh sách "chưa xếp bù" của lớp.', icon: CalendarX2 },
  ];

  return (
    <Modal
      open
      onCancel={onClose}
      width={isMobile ? '100%' : 620}
      title={request ? `Duyệt đơn hủy buổi — ${teacherName(request.teacherId)}` : 'Mời GV dạy thay'}
      footer={
        <div className="flex justify-end gap-2">
          <Button action="cancel" onClick={onClose} />
          <Button action={action === 'invite' ? 'send' : 'approve'} disabled={!upcoming} onClick={submit}>
            {action === 'invite' ? 'Duyệt & gửi lời mời' : action === 'makeup' ? 'Hủy buổi & xếp bù' : 'Duyệt & hủy buổi'}
          </Button>
        </div>
      }
    >
      <div className="mb-3 rounded-lg bg-canvas px-3 py-2 text-[13px]">
        <p className="font-medium text-ink">
          {cls.code.replace(/^GV[VN]*_/, '')} · buổi {s.no} · {dayjs(s.date).format('dddd DD/MM')} {s.startTime}–{s.endTime}
        </p>
        {request && (
          <p className="text-ink-secondary">
            {request.reasonType}: {request.reason}
          </p>
        )}
        {!upcoming && <p className="mt-1 font-medium text-danger-700">Buổi đã bắt đầu / đã qua — không xử lý được nữa. Hãy từ chối đơn kèm ghi chú.</p>}
      </div>

      {request && (
        <Radio.Group value={action} onChange={(e) => setAction(e.target.value)} className="!mb-3 !flex !flex-col gap-2">
          {OPTIONS.map((o) => (
            <Radio key={o.value} value={o.value} className={cn('!m-0 rounded-lg border p-2.5', action === o.value ? 'border-primary-400 bg-primary-50' : 'border-line')}>
              <span className="inline-flex items-center gap-1.5 font-medium">
                <o.icon size={15} aria-hidden /> {o.label}
              </span>
              <span className="block text-xs text-ink-muted">{o.hint}</span>
            </Radio>
          ))}
        </Radio.Group>
      )}

      {action === 'invite' && (
        <Form form={form} layout="vertical" requiredMark={false} initialValues={{ respondBy: dayjs(`${s.date}T${s.startTime}`).subtract(3, 'hour') }}>
          <Form.Item name="teacherIds" label="Mời giáo viên" rules={[{ required: true, message: 'Chọn ít nhất 1 GV' }]} extra="GV đang dạy lớp khác giờ đó bị khóa. Ưu tiên GV có đăng ký rảnh ca này.">
            <Select mode="multiple" options={candidates} placeholder="Chọn GV" />
          </Form.Item>
          <Form.Item name="respondBy" label="Hạn trả lời">
            <DateInput showTime disabledDate={(d) => d.isBefore(dayjs(), 'day') || d.isAfter(dayjs(s.date), 'day')} />
          </Form.Item>
          {!request && (
            <Form.Item name="message" label="Lý do (gửi kèm lời mời)">
              <TextArea rows={2} maxLength={200} />
            </Form.Item>
          )}
        </Form>
      )}
    </Modal>
  );
}
