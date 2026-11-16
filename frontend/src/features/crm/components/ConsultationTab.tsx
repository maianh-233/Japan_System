import { useMemo, useState } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { Bot, CalendarClock, MessageCircle, MessageSquarePlus, Sparkles } from 'lucide-react';
import { Button, DateInput, EmptyState, Select, StatusTag, TextArea, useNotify } from '@/components/common';
import { cn } from '@/lib/cn';
import { fmtDateTime, fromNow } from '@/lib/date';
import { useAuth } from '@/hooks/useAuth';
import { CONSULT_CHANNELS, CONSULT_RESULTS, LEAD_STATUS_OPTIONS } from '../constants';
import { useCrmStore } from '../crmStore';
import { APPOINTMENT_KIND, APPOINTMENT_STATUS, useAppointmentStore } from '../appointments/appointments';
import { CHANNEL_LABEL, useMessageStore } from '../messages/messages';
import type { ConsultChannel, ConsultResult, Lead, LeadStatus } from '../types';

interface FormValues {
  channel: ConsultChannel;
  result: ConsultResult;
  content: string;
  nextFollowUpAt?: dayjs.Dayjs;
  newStatus?: LeadStatus;
}

/** Tab 2 — Lịch sử tư vấn: thêm lần tư vấn mới + dòng thời gian */
export function ConsultationTab({ lead }: { lead: Lead }) {
  const { user } = useAuth();
  const notify = useNotify();
  const all = useCrmStore((s) => s.consultations);
  const addConsultation = useCrmStore((s) => s.addConsultation);
  const updateLead = useCrmStore((s) => s.updateLead);
  const [form] = Form.useForm<FormValues>();
  const [adding, setAdding] = useState(false);
  const [saving, setSaving] = useState(false);
  const allAppointments = useAppointmentStore((s) => s.appointments);
  const allMessages = useMessageStore((s) => s.logs);
  const appts = useMemo(() => allAppointments.filter((a) => a.leadId === lead.id).sort((a, b) => b.start.localeCompare(a.start)), [allAppointments, lead.id]);
  const msgs = useMemo(() => allMessages.filter((m) => m.leadId === lead.id), [allMessages, lead.id]);

  const items = useMemo(
    () => all.filter((c) => c.leadId === lead.id).sort((a, b) => dayjs(b.createdAt).valueOf() - dayjs(a.createdAt).valueOf()),
    [all, lead.id],
  );

  const submit = async (v: FormValues) => {
    setSaving(true);
    await new Promise((r) => setTimeout(r, 500)); // giả lập gọi API
    addConsultation({
      leadId: lead.id,
      channel: v.channel,
      result: v.result,
      content: v.content.trim(),
      staffName: user?.fullName ?? 'Nhân viên',
      nextFollowUpAt: v.nextFollowUpAt?.toISOString(),
    });
    if (v.newStatus && v.newStatus !== lead.status) updateLead(lead.id, { status: v.newStatus });
    setSaving(false);
    setAdding(false);
    form.resetFields();
    notify.toast.success('Đã thêm lịch sử tư vấn');
  };

  return (
    <div className="flex flex-col gap-4">
      {adding ? (
        <div className="rounded-[var(--radius-card)] border border-primary-200 bg-primary-50/50 p-4">
          <p className="mb-3 font-semibold text-ink">Thêm lần tư vấn</p>
          <Form<FormValues>
            form={form}
            layout="vertical"
            requiredMark
            initialValues={{ channel: 'call', result: 'interested' }}
            onFinish={submit}
            className="grid grid-cols-1 gap-x-4 sm:grid-cols-2"
          >
            <Form.Item name="channel" label="Hình thức" rules={[{ required: true }]}>
              <Select
                allowClear={false}
                showSearch={false}
                options={(Object.keys(CONSULT_CHANNELS) as ConsultChannel[]).map((k) => ({ value: k, label: CONSULT_CHANNELS[k].label }))}
              />
            </Form.Item>
            <Form.Item name="result" label="Kết quả" rules={[{ required: true }]}>
              <Select
                allowClear={false}
                showSearch={false}
                options={(Object.keys(CONSULT_RESULTS) as ConsultResult[]).map((k) => ({ value: k, label: CONSULT_RESULTS[k].label }))}
              />
            </Form.Item>
            <Form.Item name="content" label="Nội dung tư vấn" rules={[{ required: true, message: 'Nhập nội dung' }, { min: 5, message: 'Tối thiểu 5 ký tự' }]} className="sm:col-span-2">
              <TextArea maxLength={1000} placeholder="Khách hỏi gì, đã tư vấn gì, phản hồi của khách..." autoFocus />
            </Form.Item>
            <Form.Item name="nextFollowUpAt" label="Hẹn gọi lại">
              <DateInput showTime disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
            </Form.Item>
            <Form.Item name="newStatus" label="Chuyển trạng thái lead" extra="Bỏ trống = giữ nguyên">
              <Select placeholder="Giữ nguyên" options={LEAD_STATUS_OPTIONS.filter((o) => o.value !== 'won')} />
            </Form.Item>
            <div className="flex flex-col-reverse gap-2 sm:col-span-2 sm:flex-row sm:justify-end">
              <Button action="cancel" onClick={() => setAdding(false)} disabled={saving} />
              <Button action="save" htmlType="submit" loading={saving}>
                Lưu lần tư vấn
              </Button>
            </div>
          </Form>
        </div>
      ) : (
        <div className="flex items-center justify-between gap-2">
          <p className="text-[13px] text-ink-muted">{items.length} lần tư vấn</p>
          <Button icon={MessageSquarePlus} onClick={() => setAdding(true)}>
            Thêm lịch sử tư vấn
          </Button>
        </div>
      )}

      {appts.length > 0 && (
        <div className="rounded-[var(--radius-card)] border border-line">
          <p className="border-b border-line px-3 py-2 text-[13px] font-semibold text-ink">Lịch hẹn tại trung tâm ({appts.length})</p>
          <ul className="divide-y divide-line text-[13px]">
            {appts.map((a) => (
              <li key={a.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2">
                <CalendarClock size={14} className="text-ink-muted" aria-hidden />
                <span className="tabular font-medium text-ink">{dayjs(a.start).format('HH:mm DD/MM/YYYY')}</span>
                <span className="text-ink-secondary">{APPOINTMENT_KIND[a.kind].short}</span>
                <span className="ml-auto text-xs text-ink-muted">{APPOINTMENT_STATUS[a.status].label}{a.outcome ? ` · ${a.outcome}` : ''}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
      {msgs.length > 0 && (
        <details className="rounded-[var(--radius-card)] border border-line">
          <summary className="cursor-pointer px-3 py-2 text-[13px] font-semibold text-ink">Tin nhắn đã gửi ({msgs.length})</summary>
          <ul className="divide-y divide-line border-t border-line text-[13px]">
            {msgs.map((m) => (
              <li key={m.id} className="px-3 py-2">
                <p className="flex items-center gap-1.5 text-xs text-ink-muted">
                  {m.auto ? <Bot size={12} aria-hidden /> : <MessageCircle size={12} aria-hidden />}
                  {CHANNEL_LABEL[m.channel]} · {fmtDateTime(m.sentAt)} · {m.auto ? 'Tự động' : m.sentBy}
                </p>
                <p className="mt-1 whitespace-pre-line text-ink-secondary">{m.body}</p>
              </li>
            ))}
          </ul>
        </details>
      )}

      {items.length === 0 ? (
        <div className="rounded-[var(--radius-card)] border border-line">
          <EmptyState size="compact" icon={MessageSquarePlus} title="Chưa có lịch sử tư vấn" description="Ghi lại mỗi lần gọi điện, nhắn tin hoặc gặp trực tiếp với lead." />
        </div>
      ) : (
        <ol className="relative flex flex-col gap-4 before:absolute before:top-2 before:bottom-2 before:left-[19px] before:w-px before:bg-line">
          {items.map((c) => {
            const ch = CONSULT_CHANNELS[c.channel];
            const res = CONSULT_RESULTS[c.result];
            const Icon = c.system ? Sparkles : ch.icon;
            return (
              <li key={c.id} className="relative flex gap-3">
                <span
                  className={cn(
                    'z-[1] flex h-10 w-10 shrink-0 items-center justify-center rounded-full border-4 border-surface',
                    c.system ? 'bg-success-50 text-success-600' : 'bg-primary-100 text-primary-600',
                  )}
                >
                  <Icon size={16} aria-hidden />
                </span>
                <div className="min-w-0 flex-1 rounded-[var(--radius-card)] border border-line bg-surface p-3">
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                    <span className="font-medium text-ink">{c.system ? 'Hệ thống' : ch.label}</span>
                    <StatusTag tone={res.tone} icon={Icon} size="sm">
                      {res.label}
                    </StatusTag>
                    <span className="ml-auto text-xs text-ink-muted" title={fmtDateTime(c.createdAt)}>
                      {fmtDateTime(c.createdAt)} · {fromNow(c.createdAt)}
                    </span>
                  </div>
                  <p className="mt-2 whitespace-pre-line text-ink-secondary">{c.content}</p>
                  <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-muted">
                    <span>Người tư vấn: {c.staffName}</span>
                    {c.nextFollowUpAt && (
                      <span className="inline-flex items-center gap-1">
                        <CalendarClock size={12} aria-hidden /> Hẹn gọi lại {fmtDateTime(c.nextFollowUpAt)}
                      </span>
                    )}
                  </div>
                </div>
              </li>
            );
          })}
        </ol>
      )}
    </div>
  );
}
