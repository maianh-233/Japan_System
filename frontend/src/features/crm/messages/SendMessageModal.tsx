import { useEffect, useMemo, useState } from 'react';
import { Modal, Segmented } from 'antd';
import dayjs from 'dayjs';
import { CaseSensitive, Send, TriangleAlert } from 'lucide-react';
import { Button, FormField, Select, TextArea, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { useRoomStore } from '@/features/academic/rooms/roomStore';
import { summarizeInvoice, useStudentStore } from '@/features/student/studentStore';
import { useBranches } from '@/features/system/useBranches';
import { useAuth } from '@/hooks/useAuth';
import { formatCurrency } from '@/lib/format';
import { useAppointmentStore } from '../appointments/appointments';
import { courseLabel, saleName } from '../constants';
import { useCrmStore } from '../crmStore';
import {
  CHANNEL_LABEL,
  fillTemplate,
  firstName,
  missingVars,
  SCENARIOS,
  smsSegments,
  stripAccents,
  useMessageStore,
  type MessageChannel,
  type MessageScenario,
} from './messages';

export type MessageTarget = { kind: 'lead'; leadId: string } | { kind: 'student'; studentId: string };

interface Recipient {
  key: string;
  name: string;
  phone: string;
  note: string;
  leadId?: string;
}

/** Vai trò của NGƯỜI NHẬN đối với học viên (quan hệ lưu theo góc học viên: child = học viên là con của người liên hệ) */
const RECIPIENT_ROLE: Record<string, string> = { child: 'Phụ huynh', sibling: 'Anh / chị / em', spouse: 'Vợ / chồng', other: 'Người thân' };

const dateVN = (d: dayjs.Dayjs) => d.format('dddd DD/MM');

/**
 * Gửi tin Zalo / SMS theo mẫu — mở từ hồ sơ lead, hồ sơ học viên, lịch hẹn.
 * Tự điền biến từ dữ liệu thật; biến chưa có dữ liệu được báo để sửa tay trước khi gửi.
 */
export function SendMessageModal({ target, scenario, onClose }: { target: MessageTarget | null; scenario?: MessageScenario; onClose: () => void }) {
  const { user } = useAuth();
  const notify = useNotify();
  const templates = useMessageStore((s) => s.templates);
  const send = useMessageStore((s) => s.send);
  const leads = useCrmStore((s) => s.leads);
  const students = useCrmStore((s) => s.students);
  const links = useCrmStore((s) => s.links);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const appointments = useAppointmentStore((s) => s.appointments);
  const rooms = useRoomStore((s) => s.rooms);
  const branches = useBranches();

  const audience = target?.kind ?? 'lead';
  const usable = templates.filter((t) => t.active && SCENARIOS[t.scenario].audience.includes(audience));

  // ---- Người nhận ----
  const recipients: Recipient[] = useMemo(() => {
    if (!target) return [];
    if (target.kind === 'lead') {
      const l = leads.find((x) => x.id === target.leadId);
      return l ? [{ key: l.id, name: l.fullName, phone: l.phone, note: 'Lead', leadId: l.id }] : [];
    }
    const st = students.find((x) => x.id === target.studentId);
    if (!st) return [];
    const out: Recipient[] = [];
    links
      .filter((k) => k.studentId === st.id)
      .sort((a, b) => Number(Boolean(b.primary || b.billing)) - Number(Boolean(a.primary || a.billing)))
      .forEach((k) => {
        const l = leads.find((x) => x.id === k.leadId);
        if (l && k.relationship !== 'self') out.push({ key: l.id, name: l.fullName, phone: l.phone, note: `${RECIPIENT_ROLE[k.relationship] ?? 'Người thân'}${k.primary ? ' · liên hệ chính' : ''}${k.billing ? ' · nhận hóa đơn' : ''}`, leadId: l.id });
      });
    if (st.phone) out.push({ key: st.id, name: st.fullName, phone: st.phone, note: 'Học viên' });
    return out;
  }, [target, leads, students, links]);

  const [recipientKey, setRecipientKey] = useState<string>();
  const [templateId, setTemplateId] = useState<string>();
  const [channel, setChannel] = useState<MessageChannel>('zalo');
  const [body, setBody] = useState('');

  // ---- Biến ----
  const vars = useMemo(() => {
    if (!target) return {};
    const rcp = recipients.find((r) => r.key === recipientKey) ?? recipients[0];
    const v: Record<string, string | undefined> = { ten: rcp && firstName(rcp.name), ho_ten: rcp?.name };
    let branchId: string | undefined;
    if (target.kind === 'lead') {
      const l = leads.find((x) => x.id === target.leadId);
      if (!l) return v;
      branchId = l.branchId;
      v.khoa_hoc = courseLabel(l.interest) ?? l.interest;
      v.sale = saleName(l.assigneeId) ?? user?.fullName;
      // Lịch hẹn sắp tới (ưu tiên đúng loại với mẫu)
      const kind = scenarioOf(templateId) === 'consult_reminder' ? 'consult' : scenarioOf(templateId) === 'test_reminder' ? 'test' : undefined;
      const ap = appointments
        .filter((a) => a.leadId === l.id && a.status === 'scheduled' && dayjs(a.end).isAfter(dayjs()) && (!kind || a.kind === kind))
        .sort((a, b) => a.start.localeCompare(b.start))[0];
      if (ap) {
        v.ngay = dateVN(dayjs(ap.start));
        v.gio = dayjs(ap.start).format('HH:mm');
        v.phong = rooms.find((r) => r.id === ap.roomId)?.name;
        branchId = ap.branchId;
      }
    } else {
      const st = students.find((x) => x.id === target.studentId);
      if (!st) return v;
      branchId = st.branchId;
      v.hoc_vien = st.fullName;
      const mine = enrollments.filter((e) => e.studentId === st.id && (e.status === 'studying' || e.status === 'reserved'));
      const withDebt = mine
        .map((e) => ({ e, inv: invoices.find((i) => i.enrollmentId === e.id) }))
        .map((x) => ({ ...x, sum: x.inv && summarizeInvoice(x.inv) }))
        .filter((x) => x.sum && x.sum.remaining > 0)
        .sort((a, b) => (a.inv!.dueDate ?? '').localeCompare(b.inv!.dueDate ?? ''))[0];
      const enr = withDebt?.e ?? mine[0];
      const cls = enr && classes.find((c) => c.id === enr.classId);
      if (cls) {
        v.lop = cls.code.replace(/^GV[VN]*_/, '');
        branchId = cls.branchId;
        const next = sessions.filter((s) => s.classId === cls.id && s.status === 'scheduled' && dayjs(`${s.date}T${s.endTime}`).isAfter(dayjs())).sort((a, b) => `${a.date}${a.startTime}`.localeCompare(`${b.date}${b.startTime}`))[0];
        if (next) {
          v.ngay = dateVN(dayjs(next.date));
          v.gio = `${next.startTime}–${next.endTime}`;
        }
      }
      if (withDebt?.sum) {
        v.so_tien = formatCurrency(withDebt.sum.remaining);
        v.han_dong = dayjs(withDebt.inv!.dueDate).format('DD/MM/YYYY');
      }
    }
    const b = branches.find((x) => x.id === branchId);
    v.chi_nhanh = b?.name;
    v.dia_chi = b?.address;
    v.hotline = b?.phone;
    return v;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [target, recipientKey, recipients, templateId, leads, students, enrollments, invoices, classes, sessions, appointments, rooms, branches, user]);

  function scenarioOf(id?: string) {
    return templates.find((t) => t.id === id)?.scenario;
  }

  // Mở mới → chọn người nhận + mẫu mặc định theo tình huống
  useEffect(() => {
    if (!target) return;
    setRecipientKey(recipients[0]?.key);
    const t = usable.find((x) => x.scenario === scenario) ?? usable[0];
    setTemplateId(t?.id);
    setChannel(t?.channel ?? 'zalo');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [target]);

  // Đổi mẫu / người nhận → điền lại nội dung
  useEffect(() => {
    const t = templates.find((x) => x.id === templateId);
    if (t) setBody(fillTemplate(t.body, vars));
  }, [templateId, vars, templates]);

  const rcp = recipients.find((r) => r.key === recipientKey);
  const missing = missingVars(body);
  const sms = smsSegments(body);
  const tpl = templates.find((x) => x.id === templateId);

  const submit = () => {
    if (!rcp || !body.trim() || missing.length) return;
    send({
      channel,
      scenario: tpl?.scenario ?? 'custom',
      templateId,
      toName: rcp.name,
      toPhone: rcp.phone,
      leadId: target?.kind === 'lead' ? target.leadId : rcp.leadId,
      studentId: target?.kind === 'student' ? target.studentId : undefined,
      body: body.trim(),
      sentBy: user?.fullName ?? 'Nhân viên',
    });
    notify.toast.success(`Đã gửi ${CHANNEL_LABEL[channel]} cho ${rcp.name}`);
    onClose();
  };

  return (
    <Modal
      open={Boolean(target)}
      onCancel={onClose}
      title="Gửi tin nhắn"
      width={620}
      destroyOnHidden
      footer={
        <div className="flex justify-end gap-2">
          <Button action="cancel" onClick={onClose} />
          <Button icon={Send} disabled={!rcp || !body.trim() || missing.length > 0} onClick={submit}>
            Gửi {CHANNEL_LABEL[channel]}
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        {recipients.length === 0 ? (
          <p className="rounded-lg bg-warning-50 px-3 py-2 text-[13px] text-warning-700">Không có số điện thoại nào để gửi — thêm SĐT cho học viên hoặc người thân trước.</p>
        ) : (
          <FormField label="Gửi tới">
            <Select
              value={recipientKey}
              onChange={(v) => setRecipientKey(v as string)}
              allowClear={false}
              showSearch={false}
              options={recipients.map((r) => ({ value: r.key, label: `${r.name} · ${r.phone} (${r.note})` }))}
            />
          </FormField>
        )}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_auto]">
          <FormField label="Mẫu tin">
            <Select
              value={templateId}
              onChange={(v) => {
                setTemplateId(v as string);
                const t = templates.find((x) => x.id === v);
                if (t) setChannel(t.channel);
              }}
              allowClear={false}
              options={usable.map((t) => ({ value: t.id, label: `${SCENARIOS[t.scenario].label} — ${t.name}` }))}
            />
          </FormField>
          <FormField label="Kênh">
            <Segmented<MessageChannel> value={channel} onChange={setChannel} options={[{ value: 'zalo', label: 'Zalo' }, { value: 'sms', label: 'SMS' }]} />
          </FormField>
        </div>
        <FormField
          label="Nội dung"
          hint={
            channel === 'sms'
              ? `${sms.chars} ký tự · ${sms.segments} tin SMS${sms.unicode ? ' (có dấu: 70 ký tự / tin)' : ''}`
              : 'Đã tự điền thông tin — có thể sửa trước khi gửi'
          }
        >
          <TextArea rows={6} value={body} onChange={(e) => setBody(e.target.value)} maxLength={1000} />
        </FormField>
        {channel === 'sms' && sms.unicode && (
          <Button icon={CaseSensitive} size="sm" variant="ghost" className="self-start" onClick={() => setBody(stripAccents(body))}>
            Chuyển sang không dấu ({smsSegments(stripAccents(body)).segments} tin)
          </Button>
        )}
        {missing.length > 0 && (
          <p className="flex items-start gap-2 rounded-lg bg-warning-50 px-3 py-2 text-[13px] text-warning-700">
            <TriangleAlert size={15} className="mt-0.5 shrink-0" aria-hidden />
            <span>
              Chưa có dữ liệu cho {missing.map((m) => `{${m}}`).join(', ')}
              {target?.kind === 'lead' && missing.some((m) => ['ngay', 'gio', 'phong'].includes(m)) ? ' — lead chưa có lịch hẹn sắp tới' : ''}
              {missing.includes('so_tien') ? ' — học viên không còn nợ học phí' : ''}. Sửa tay trong nội dung để gửi.
            </span>
          </p>
        )}
      </div>
    </Modal>
  );
}
