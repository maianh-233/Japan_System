import { useMemo } from 'react';
import { Checkbox, Form, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { Info } from 'lucide-react';
import { ModalForm, Select, TextArea, TimeInput, useNotify } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { useClassStore } from '../classes/classStore';
import * as classApi from '@/features/academic/classes/classApi';
import { USE_API } from '@/lib/api/config';
import { teacherName } from '../classes/constants';
import type { ClassInfo, ClassSession, IncidentAction, IncidentReason, TeacherAttendance } from '../classes/types';
import { busyStaff, INCIDENT_ACTIONS, INCIDENT_REASONS, staffOf, takeoverOptions } from './sessionOps';

interface Values {
  at: Dayjs;
  reason: IncidentReason;
  detail?: string;
  action: IncidentAction;
  /** GV vào dạy thay (action = substitute) */
  substituteId?: string;
  /** Trợ giảng dạy tiếp (action = assistant) */
  assistantId?: string;
  takeoverAt?: Dayjs;
  notify?: boolean;
}

const atTime = (date: string, t: string) => dayjs(`${date}T${t}`);

/**
 * GV gặp sự cố GIỮA buổi (ốm, việc gấp...). Nhân viên ghi nhận + chọn cách xử lý:
 *  - GV khác vào dạy tiếp → chấm công tách đôi (GV gốc tới giờ sự cố, GV thay từ lúc vào lớp)
 *  - Trợ giảng dạy tiếp
 *  - Cho nghỉ sớm → nhắc xếp học bù phần còn lại
 */
export function IncidentModal({ cls, session, onClose }: { cls: ClassInfo; session: ClassSession | null; onClose: () => void }) {
  const { user } = useAuth();
  const notify = useNotify();
  const [form] = Form.useForm<Values>();
  const sessions = useClassStore((s) => s.sessions);
  const classes = useClassStore((s) => s.classes);
  const updateSession = useClassStore((s) => s.updateSession);
  const action = Form.useWatch('action', form);
  const at = Form.useWatch('at', form);

  const s = session;
  const staff = s ? staffOf(s, cls) : [];
  const leads = staff.filter((x) => x.role !== 'assistant').map((x) => x.id);
  const assistants = staff.filter((x) => x.role === 'assistant').map((x) => x.id);

  // Người đang bận trong khoảng còn lại của buổi
  const busy = useMemo(
    () => (s ? busyStaff(s.date, at?.format('HH:mm') ?? s.startTime, s.endTime, sessions, classes, s.id) : new Map<string, string>()),
    [s, at, sessions, classes],
  );

  if (!s) return null;

  const now = dayjs();
  const start = atTime(s.date, s.startTime);
  const end = atTime(s.date, s.endTime);
  const defaultAt = now.isAfter(start) && now.isBefore(end) ? now : start.add(Math.round(end.diff(start, 'minute') / 2), 'minute');

  const submit = async (v: Values) => {
    const takeoverId = v.action === 'substitute' ? v.substituteId : v.action === 'assistant' ? v.assistantId : undefined;
    const t = v.at.format('HH:mm');
    const taIn = (v.takeoverAt ?? v.at.add(10, 'minute')).format('HH:mm');
    if (USE_API) {
      // Server ghi sự cố + chỉnh chấm công GV (GV dừng dạy, người thay vào lớp) + nhật ký hỗ trợ
      await classApi.reportIncident(s.id, { at: t, reason: v.reason, detail: v.detail?.trim() || undefined, action: v.action, takeoverId, notified: Boolean(v.notify), takeoverAt: taIn });
      if (v.action === 'pause') notify.warning({ title: 'Lớp đang tạm dừng', description: 'Khi ổn định, mở chi tiết buổi để cho lớp học tiếp hoặc cho nghỉ.' });
      if (v.action === 'end_early') notify.warning({ title: 'Đã ghi nhận lớp nghỉ sớm', description: 'Nhớ xếp buổi học bù cho phần còn lại ở Lớp học → Buổi học.' });
      return;
    }
    const reason = INCIDENT_REASONS[v.reason].label;
    const stamp = new Date().toISOString();
    // Chấm công hiện có (hoặc mặc định theo người đứng lớp)
    const saved = new Map((s.teacherAttendance ?? []).map((x) => [x.teacherId, x]));
    let rows: TeacherAttendance[] = staff.map(
      (x) => saved.get(x.id) ?? { teacherId: x.id, role: x.role, status: 'present', checkIn: s.startTime, source: 'staff', checkedAt: stamp },
    );
    // GV gốc dừng dạy lúc xảy ra sự cố
    rows = rows.map((r) => (leads.includes(r.teacherId) ? { ...r, checkOut: t, note: `${reason} — dừng dạy lúc ${t}`, source: 'staff', checkedAt: stamp } : r));
    if (v.action === 'substitute' && takeoverId) {
      rows = [
        ...rows.filter((r) => r.teacherId !== takeoverId),
        { teacherId: takeoverId, role: 'substitute', status: 'present', checkIn: taIn, checkOut: undefined, note: `Dạy thay từ ${taIn} (GV gặp sự cố)`, source: 'staff', checkedAt: stamp },
      ];
    }
    if (v.action === 'assistant' && takeoverId) {
      rows = rows.map((r) => (r.teacherId === takeoverId ? { ...r, note: `Đứng lớp thay GV từ ${t}` } : r));
    }
    if (v.action === 'end_early') {
      rows = rows.map((r) => ({ ...r, checkOut: t }));
    }
    const reporter = user ? `${user.fullName} (${user.roleName})` : 'Nhân viên';
    updateSession(s.id, {
      incident: {
        at: t,
        reason: v.reason,
        detail: v.detail?.trim() || undefined,
        action: v.action,
        takeoverId,
        notified: v.notify,
        reportedBy: reporter,
        reportedAt: stamp,
      },
      teacherAttendance: rows,
      note: v.action === 'end_early' ? `Nghỉ sớm từ ${t} do GV gặp sự cố — cần học bù phần còn lại` : v.action === 'pause' ? `Tạm dừng lớp từ ${t} do GV gặp sự cố` : s.note,
      supportLog: [
        ...(s.supportLog ?? []),
        {
          id: `SL-${Date.now()}`,
          kind: 'incident',
          note: `${reason} lúc ${t}${v.detail?.trim() ? ` (${v.detail.trim()})` : ''} → ${INCIDENT_ACTIONS[v.action].label}${takeoverId ? `: ${teacherName(takeoverId)}` : ''}.`,
          by: reporter,
          at: stamp,
        },
      ],
    });
    if (v.action === 'pause') notify.warning({ title: 'Lớp đang tạm dừng', description: 'Khi ổn định, mở chi tiết buổi để cho lớp học tiếp hoặc cho nghỉ.' });
    if (v.action === 'end_early') notify.warning({ title: 'Đã ghi nhận lớp nghỉ sớm', description: 'Nhớ xếp buổi học bù cho phần còn lại ở Lớp học → Buổi học.' });
  };

  const actionOptions = (Object.keys(INCIDENT_ACTIONS) as IncidentAction[]).filter((k) => k !== 'assistant' || assistants.length);

  return (
    <ModalForm<Values>
      key={s.id}
      form={form}
      open
      onClose={onClose}
      width={620}
      title="Báo sự cố giáo viên trong buổi"
      description={`${cls.code} · ${dayjs(s.date).format('DD/MM')} ${s.startTime}–${s.endTime} · GV: ${leads.map(teacherName).join(', ')}`}
      submitAction="save"
      submitText="Ghi nhận & xử lý"
      successMessage="Đã ghi nhận sự cố"
      initialValues={{
        at: defaultAt,
        reason: 'faint',
        action: 'pause',
        assistantId: assistants[0],
        notify: true,
      }}
      onSubmit={submit}
    >
      <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
        <Form.Item
          name="at"
          label="Xảy ra lúc"
          rules={[
            { required: true, message: 'Chọn giờ' },
            {
              validator: (_, v: Dayjs) => {
                if (!v) return Promise.resolve();
                const m = atTime(s.date, v.format('HH:mm'));
                return m.isBefore(start) || m.isAfter(end) ? Promise.reject(new Error(`Trong giờ học ${s.startTime}–${s.endTime}`)) : Promise.resolve();
              },
            },
          ]}
        >
          <TimeInput minuteStep={5} />
        </Form.Item>
        <Form.Item name="reason" label="Lý do" rules={[{ required: true }]}>
          <Select options={(Object.keys(INCIDENT_REASONS) as IncidentReason[]).map((k) => ({ value: k, label: INCIDENT_REASONS[k].label }))} showSearch={false} allowClear={false} />
        </Form.Item>
      </div>
      <Form.Item name="detail" label="Chi tiết / đã làm gì">
        <TextArea rows={2} maxLength={300} placeholder="VD: GV ngất trên bục giảng, đã sơ cứu, gọi người nhà / 115; HV ra sảnh chờ" />
      </Form.Item>

      <Form.Item name="action" label="Xử lý phần còn lại của buổi" rules={[{ required: true }]}>
        <Radio.Group className="flex flex-col gap-1.5">
          {actionOptions.map((k) => (
            <Radio key={k} value={k}>
              <span className="font-medium">{INCIDENT_ACTIONS[k].label}</span> <span className="text-[13px] text-ink-muted">— {INCIDENT_ACTIONS[k].hint}</span>
            </Radio>
          ))}
        </Radio.Group>
      </Form.Item>

      {action === 'substitute' && (
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-[1fr_140px]">
          <Form.Item name="substituteId" label="GV vào dạy thay" rules={[{ required: true, message: 'Chọn giáo viên' }]} extra="GV đang dạy lớp khác cùng giờ bị khóa.">
            <Select options={takeoverOptions(cls, busy, leads)} placeholder="Chọn giáo viên rảnh" />
          </Form.Item>
          <Form.Item name="takeoverAt" label="Vào lớp lúc">
            <TimeInput minuteStep={5} placeholder={at ? at.add(10, 'minute').format('HH:mm') : 'HH:mm'} />
          </Form.Item>
        </div>
      )}
      {action === 'assistant' && (
        <Form.Item name="assistantId" label="Trợ giảng dạy tiếp" rules={[{ required: true, message: 'Chọn trợ giảng' }]}>
          <Select options={assistants.map((id) => ({ value: id, label: teacherName(id) }))} showSearch={false} allowClear={false} />
        </Form.Item>
      )}
      {action === 'pause' && (
        <p className="mb-4 flex gap-2 rounded-lg border border-danger-100 bg-danger-50 px-3 py-2 text-[13px] text-danger-700">
          <Info size={16} className="mt-0.5 shrink-0" aria-hidden />
          Ưu tiên an toàn của giáo viên. Giờ dạy của GV tính tới lúc dừng. Sau đó mở chi tiết buổi → "Cho học tiếp" (GV / người thay vào lớp) hoặc "Cho nghỉ, xếp học bù".
        </p>
      )}
      {action === 'end_early' && (
        <p className="mb-4 flex gap-2 rounded-lg border border-warning-100 bg-warning-50 px-3 py-2 text-[13px] text-warning-700">
          <Info size={16} className="mt-0.5 shrink-0" aria-hidden />
          Giờ dạy tính tới lúc xảy ra sự cố. Sau khi lưu, xếp buổi học bù ở Lớp học → Buổi học.
        </p>
      )}

      <Form.Item name="notify" valuePropName="checked" className="!mb-0">
        <Checkbox>Gửi thông báo cho học viên / phụ huynh (Cổng + Zalo)</Checkbox>
      </Form.Item>
    </ModalForm>
  );
}
