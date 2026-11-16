import { useMemo } from 'react';
import { Form, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { CircleAlert, CircleCheck } from 'lucide-react';
import { DateInput, ModalForm, Select, TextArea, TimeInput } from '@/components/common';
import { useRoomStore } from '@/features/academic/rooms/roomStore';
import { useHrStore } from '@/features/hr/hrStore';
import { useAuth } from '@/hooks/useAuth';
import { ALL_BRANCHES } from '@/types/auth';
import { useCrmStore } from '../crmStore';
import { APPOINTMENT_KIND, DEFAULT_DURATION, findConflicts, REMIND_BEFORE_MIN, useAppointmentStore, type Appointment, type AppointmentKind } from './appointments';

interface Values {
  leadId: string;
  kind: AppointmentKind;
  date: Dayjs;
  time: Dayjs;
  duration: number;
  roomId?: string;
  staffId: string;
  note?: string;
}

const DURATIONS = [30, 45, 60, 90, 120].map((m) => ({ value: m, label: m < 60 ? `${m} phút` : `${m / 60} giờ`.replace('.5', ',5') }));
/** Phòng ưu tiên cho tư vấn / test: phòng họp nhỏ trước */
const ROOM_ORDER = { meeting: 0, lab: 1, classroom: 2 } as Record<string, number>;

/**
 * Đặt / đổi lịch hẹn tư vấn hoặc test tại trung tâm.
 * Trùng PHÒNG / NGƯỜI TIẾP / lead đã có hẹn khác cùng giờ → chặn lưu.
 */
export function BookAppointmentModal({ open, onClose, leadId, editing, defaultKind = 'consult', onSaved }: { open: boolean; onClose: () => void; leadId?: string; editing?: Appointment; defaultKind?: AppointmentKind; onSaved?: (a: Appointment) => void }) {
  const { user, branchId } = useAuth();
  const [form] = Form.useForm<Values>();
  const leads = useCrmStore((s) => s.leads);
  const addConsultation = useCrmStore((s) => s.addConsultation);
  const updateLead = useCrmStore((s) => s.updateLead);
  const rooms = useRoomStore((s) => s.rooms);
  const employees = useHrStore((s) => s.employees);
  const appointments = useAppointmentStore((s) => s.appointments);
  const add = useAppointmentStore((s) => s.add);
  const update = useAppointmentStore((s) => s.update);

  const v = Form.useWatch([], form) as Partial<Values> | undefined;
  const lead = leads.find((l) => l.id === (v?.leadId ?? editing?.leadId ?? leadId));
  const apBranch = editing?.branchId ?? lead?.branchId ?? (branchId !== ALL_BRANCHES ? branchId : undefined);

  const start = v?.date && v?.time ? v.date.hour(v.time.hour()).minute(v.time.minute()).second(0).millisecond(0) : undefined;
  const end = start && v?.duration ? start.add(v.duration, 'minute') : undefined;

  const conflicts = useMemo(
    () =>
      start && end && apBranch
        ? findConflicts({ id: editing?.id, leadId: v?.leadId, branchId: apBranch, roomId: v?.roomId, staffId: v?.staffId, start, end }, { appointments, rooms, employees, leadName: (id) => leads.find((l) => l.id === id)?.fullName })
        : [],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [start?.valueOf(), end?.valueOf(), apBranch, v?.roomId, v?.staffId, v?.leadId, appointments, rooms, employees, leads, editing?.id],
  );
  const blocking = conflicts;

  /** Phòng bận / trống trong khung giờ đang chọn (để gợi ý ngay trong ô chọn) */
  const roomOptions = rooms
    .filter((r) => r.branchId === apBranch && r.status !== 'inactive')
    .sort((a, b) => (ROOM_ORDER[a.type] ?? 9) - (ROOM_ORDER[b.type] ?? 9) || a.name.localeCompare(b.name))
    .map((r) => {
      const busy = start && end ? findConflicts({ id: editing?.id, branchId: r.branchId, roomId: r.id, start, end }, { appointments, rooms, employees }).length > 0 : false;
      return { value: r.id, label: `${r.name} · ${r.capacity} chỗ${r.status === 'maintenance' ? ' · bảo trì' : busy ? ' · BẬN' : start ? ' · trống' : ''}` };
    });

  const staffOptions = employees
    .filter((e) => e.branchId === apBranch && e.status !== 'quit' && (['sales', 'reception', 'academic', 'board'].includes(e.department) || (v?.kind === 'test' && e.kind === 'teacher')))
    .map((e) => {
      const busy = start && end ? findConflicts({ id: editing?.id, branchId: apBranch!, staffId: e.id, start, end }, { appointments, rooms, employees }).length > 0 : false;
      return { value: e.id, label: `${e.fullName} · ${e.position}${busy ? ' · BẬN' : ''}` };
    });

  const leadOptions = leads
    .filter((l) => !l.hidden && l.status !== 'lost' && (branchId === ALL_BRANCHES || l.branchId === branchId))
    .map((l) => ({ value: l.id, label: `${l.fullName} · ${l.phone} (${l.code})` }));

  const initial: Partial<Values> = editing
    ? { leadId: editing.leadId, kind: editing.kind, date: dayjs(editing.start), time: dayjs(editing.start), duration: dayjs(editing.end).diff(dayjs(editing.start), 'minute'), roomId: editing.roomId, staffId: editing.staffId, note: editing.note }
    : { leadId, kind: defaultKind, date: dayjs().add(1, 'day'), time: dayjs().hour(18).minute(0), duration: DEFAULT_DURATION[defaultKind], staffId: lead?.assigneeId };

  return (
    <ModalForm<Values>
      key={editing?.id ?? leadId ?? 'new'}
      form={form}
      open={open}
      onClose={onClose}
      width={640}
      columns={2}
      title={editing ? `Đổi lịch hẹn ${editing.code}` : 'Đặt lịch hẹn tại trung tâm'}
      description={`Hệ thống tự nhắc lead và người tiếp trước ${REMIND_BEFORE_MIN / 60} giờ`}
      submitAction="save"
      submitText={editing ? 'Lưu lịch mới' : 'Đặt lịch'}
      successMessage={editing ? 'Đã đổi lịch hẹn' : 'Đã đặt lịch hẹn'}
      initialValues={initial}
      onSubmit={async (val) => {
        if (!start || !end || !apBranch) throw new Error('Chọn ngày giờ');
        if (blocking.length) throw new Error('Lịch bị trùng — đổi giờ, phòng hoặc người tiếp');
        await new Promise((r) => setTimeout(r, 300));
        const base = { leadId: val.leadId, kind: val.kind, branchId: apBranch, roomId: val.roomId, staffId: val.staffId, start: start.toISOString(), end: end.toISOString(), note: val.note?.trim() || undefined };
        if (editing) {
          // Đổi giờ → nhắc lại từ đầu
          update(editing.id, { ...base, remindedAt: dayjs(editing.start).isSame(start) ? editing.remindedAt : undefined });
          onSaved?.({ ...editing, ...base });
          return;
        }
        const a = add({ ...base, createdBy: user?.fullName ?? 'Nhân viên' });
        const l = leads.find((x) => x.id === val.leadId);
        addConsultation({
          leadId: val.leadId,
          channel: 'call',
          result: val.kind === 'test' ? 'test_booked' : 'interested',
          content: `Đặt lịch ${APPOINTMENT_KIND[val.kind].label.toLowerCase()} ${start.format('HH:mm DD/MM/YYYY')} (${a.code}).${val.note ? ` ${val.note.trim()}` : ''}`,
          staffName: user?.fullName ?? 'Nhân viên',
        });
        if (val.kind === 'test' && l && (l.status === 'new' || l.status === 'consulting')) updateLead(l.id, { status: 'test_scheduled' });
        onSaved?.(a);
      }}
    >
      <Form.Item name="leadId" label="Lead" rules={[{ required: true, message: 'Chọn lead' }]} className="md:col-span-2">
        <Select
          placeholder="Tìm theo tên, SĐT..."
          options={leadOptions}
          disabled={Boolean(leadId || editing)}
          onChange={(id) => form.setFieldsValue({ staffId: leads.find((l) => l.id === id)?.assigneeId, roomId: undefined })}
        />
      </Form.Item>
      <Form.Item name="kind" label="Loại hẹn" rules={[{ required: true }]} className="md:col-span-2">
        <Radio.Group optionType="button" buttonStyle="solid" onChange={(e) => form.setFieldsValue({ duration: DEFAULT_DURATION[e.target.value as AppointmentKind] })} options={(Object.keys(APPOINTMENT_KIND) as AppointmentKind[]).map((k) => ({ value: k, label: APPOINTMENT_KIND[k].label }))} />
      </Form.Item>
      <Form.Item name="date" label="Ngày" rules={[{ required: true, message: 'Chọn ngày' }]}>
        <DateInput disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
      </Form.Item>
      <div className="grid grid-cols-2 gap-x-3">
        <Form.Item name="time" label="Giờ" rules={[{ required: true, message: 'Chọn giờ' }]}>
          <TimeInput minuteStep={15} />
        </Form.Item>
        <Form.Item name="duration" label="Thời lượng" rules={[{ required: true }]}>
          <Select options={DURATIONS} allowClear={false} showSearch={false} />
        </Form.Item>
      </div>
      <Form.Item name="staffId" label={v?.kind === 'test' ? 'Người coi test' : 'Người tiếp'} rules={[{ required: true, message: 'Chọn người' }]}>
        <Select placeholder="Chọn nhân viên" options={staffOptions} />
      </Form.Item>
      <Form.Item name="roomId" label="Phòng" extra="Bỏ trống nếu tiếp tại quầy">
        <Select placeholder="Chọn phòng" options={roomOptions} />
      </Form.Item>
      <Form.Item name="note" label="Ghi chú" className="md:col-span-2">
        <TextArea rows={2} placeholder="VD: Khách đi cùng con 10 tuổi, muốn xem lớp thiếu nhi" />
      </Form.Item>

      <div className="md:col-span-2">
        {start && end && (
          conflicts.length ? (
            <ul className="flex flex-col gap-1 rounded-lg bg-danger-50 px-3 py-2 text-[13px]">
              {conflicts.map((c, i) => (
                <li key={i} className="flex items-start gap-2 text-danger-700">
                  <CircleAlert size={15} className="mt-0.5 shrink-0" aria-hidden />
                  {c.message}
                </li>
              ))}
            </ul>
          ) : (
            <p className="flex items-center gap-2 rounded-lg bg-success-50 px-3 py-2 text-[13px] text-success-700">
              <CircleCheck size={15} aria-hidden /> Không trùng lịch · {start.format('HH:mm')}–{end.format('HH:mm')} {start.format('dddd DD/MM')}
            </p>
          )
        )}
      </div>
    </ModalForm>
  );
}
