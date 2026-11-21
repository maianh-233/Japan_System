import { useEffect, useState } from 'react';
import { Drawer, Form, Tabs } from 'antd';
import { CalendarPlus, EyeOff, GraduationCap, Info, MessageCircle, MessagesSquare } from 'lucide-react';
import { Button, StatusTag, useNotify } from '@/components/common';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { fmtDateTime, fromNow } from '@/lib/date';
import { USE_API } from '@/lib/api/config';
import { errorText, loadLead, updateLead as saveLead } from '../crmApi';
import { loadMessages } from '../messages/messageApi';
import { useCrmStore } from '../crmStore';
import { useHideLead } from '../useHideLead';
import type { Lead } from '../types';
import { BookAppointmentModal } from '../appointments/BookAppointmentModal';
import { SendMessageModal } from '../messages/SendMessageModal';
import { ConsultationTab } from './ConsultationTab';
import { EnrollLeadModal } from './EnrollLeadModal';
import { formToLead, LeadFormFields, leadToForm, type LeadFormValues } from './LeadFormFields';
import { LinkedStudentsTab } from './LinkedStudentsTab';

export type LeadTab = 'info' | 'history' | 'students';

interface Props {
  leadId: string | null;
  initialTab?: LeadTab;
  onClose: () => void;
  /** Mở lead khác (vd: lead bị trùng SĐT) */
  onOpenLead: (id: string) => void;
}

/** Chi tiết lead: Tab Thông tin (xem + sửa) · Lịch sử tư vấn · Học viên liên kết */
export function LeadDetailDrawer({ leadId, initialTab = 'info', onClose, onOpenLead }: Props) {
  const { isMobile } = useBreakpoint();
  const lead = useCrmStore((s) => s.leads.find((l) => l.id === leadId));
  const consultCount = useCrmStore((s) => s.consultations.filter((c) => c.leadId === leadId).length);
  const studentCount = useCrmStore((s) => s.links.filter((k) => k.leadId === leadId).length);
  const notify = useNotify();
  const [tab, setTab] = useState<LeadTab>(initialTab);
  const { toggleHidden, confirmDialog } = useHideLead();
  const [enrolling, setEnrolling] = useState(false);
  const [messaging, setMessaging] = useState(false);
  const [booking, setBooking] = useState(false);

  useEffect(() => {
    if (leadId) setTab(initialTab);
  }, [leadId, initialTab]);

  // Chế độ API: mở lead → nạp thông tin mới nhất + lịch sử tư vấn + HV liên kết + tin đã gửi
  useEffect(() => {
    if (!USE_API || !leadId) return;
    loadLead(leadId).catch((e) => notify.toast.error(errorText(e, 'Không tải được chi tiết lead')));
    loadMessages({ leadId }).catch(() => undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [leadId]);

  return (
    <Drawer
      open={Boolean(leadId)}
      onClose={onClose}
      width={isMobile ? '100%' : 780}
      destroyOnHidden
      styles={{ body: { paddingTop: 0 } }}
      title={
        lead && (
          <div className="flex min-w-0 flex-col gap-1 pr-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="truncate text-base font-semibold">{lead.fullName}</span>
              <StatusTag group="lead" status={lead.status} size="sm" />
              {lead.hidden && (
                <StatusTag tone="neutral" icon={EyeOff} size="sm">
                  Đã ẩn
                </StatusTag>
              )}
            </div>
            <span className="text-xs font-normal text-ink-muted">
              {lead.code} · {lead.phone}
            </span>
          </div>
        )
      }
      extra={
        lead && (
          <div className="flex gap-2">
            {!lead.hidden && (
              <Button icon={MessageCircle} size="sm" variant="outline" collapseOnMobile onClick={() => setMessaging(true)}>
                Nhắn tin
              </Button>
            )}
            {!lead.hidden && lead.status !== 'won' && lead.status !== 'lost' && (
              <Button icon={CalendarPlus} size="sm" variant="outline" collapseOnMobile onClick={() => setBooking(true)}>
                Đặt lịch hẹn
              </Button>
            )}
            {!lead.hidden && (
              <Button icon={GraduationCap} size="sm" collapseOnMobile onClick={() => setEnrolling(true)}>
                Chốt & ghi danh
              </Button>
            )}
            <Button
              icon={EyeOff}
              tone="neutral"
              variant="outline"
              size="sm"
              collapseOnMobile
              onClick={() => toggleHidden(lead)}
            >
              {lead.hidden ? 'Bỏ ẩn' : 'Ẩn lead'}
            </Button>
          </div>
        )
      }
    >
      {lead && (
        <Tabs
          activeKey={tab}
          onChange={(k) => setTab(k as LeadTab)}
          items={[
            {
              key: 'info',
              label: (
                <span className="inline-flex items-center gap-1.5">
                  <Info size={16} aria-hidden /> Thông tin
                </span>
              ),
              children: <InfoTab key={lead.id} lead={lead} onOpenLead={onOpenLead} />,
            },
            {
              key: 'history',
              label: (
                <span className="inline-flex items-center gap-1.5">
                  <MessagesSquare size={16} aria-hidden /> Lịch sử tư vấn ({consultCount})
                </span>
              ),
              children: <ConsultationTab lead={lead} />,
            },
            {
              key: 'students',
              label: (
                <span className="inline-flex items-center gap-1.5">
                  <GraduationCap size={16} aria-hidden /> Học viên ({studentCount})
                </span>
              ),
              children: <LinkedStudentsTab lead={lead} />,
            },
          ]}
        />
      )}
      {confirmDialog}
      {lead && enrolling && <EnrollLeadModal lead={lead} open onClose={() => setEnrolling(false)} />}
      <SendMessageModal target={lead && messaging ? { kind: 'lead', leadId: lead.id } : null} scenario={lead?.status === 'new' ? 'welcome' : lead?.status === 'test_scheduled' ? 'test_reminder' : undefined} onClose={() => setMessaging(false)} />
      {lead && <BookAppointmentModal open={booking} leadId={lead.id} defaultKind={lead.status === 'consulting' ? 'test' : 'consult'} onClose={() => setBooking(false)} />}
    </Drawer>
  );
}

// ---------------- Tab 1: Thông tin ----------------

function InfoTab({ lead, onOpenLead }: { lead: Lead; onOpenLead: (id: string) => void }) {
  const [form] = Form.useForm<LeadFormValues>();
  const notify = useNotify();
  const updateLead = useCrmStore((s) => s.updateLead);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);

  const save = async (values: LeadFormValues) => {
    setSaving(true);
    const data = formToLead(values);
    if (USE_API) {
      try {
        const { warnings } = await saveLead(lead.id, data);
        setDirty(false);
        notify.success({ title: 'Đã cập nhật lead', description: `${lead.code} — ${data.fullName}` });
        warnings.forEach((w) => notify.toast.warning(w.message));
      } catch (e) {
        notify.toast.error(errorText(e, 'Không lưu được lead'));
      } finally {
        setSaving(false);
      }
      return;
    }
    await new Promise((r) => setTimeout(r, 600)); // giả lập gọi API
    updateLead(lead.id, { ...data, lostReason: data.status === 'lost' ? data.lostReason : undefined });
    setSaving(false);
    setDirty(false);
    notify.success({ title: 'Đã cập nhật lead', description: `${lead.code} — ${data.fullName}` });
  };

  const meta: [string, string][] = [
    ['Mã lead', lead.code],
    ['Ngày tạo', `${fmtDateTime(lead.createdAt)} (${fromNow(lead.createdAt)})`],
    ...(lead.createdBy ? [['Người tạo', lead.createdBy] as [string, string]] : []),
    ['Liên hệ gần nhất', lead.lastContactAt ? `${fmtDateTime(lead.lastContactAt)} (${fromNow(lead.lastContactAt)})` : 'Chưa liên hệ'],
  ];
  if (lead.hidden && lead.hiddenAt) meta.push(['Ẩn lúc', fmtDateTime(lead.hiddenAt)]);

  return (
    <div className="flex flex-col gap-5">
      <dl className="grid grid-cols-1 gap-x-6 gap-y-2 rounded-[var(--radius-card)] bg-page p-4 text-[13px] sm:grid-cols-2">
        {meta.map(([k, v]) => (
          <div key={k} className="flex gap-2">
            <dt className="w-32 shrink-0 text-ink-muted">{k}</dt>
            <dd className="min-w-0 font-medium text-ink">{v}</dd>
          </div>
        ))}
      </dl>

      <Form
        form={form}
        layout="vertical"
        requiredMark
        initialValues={leadToForm(lead)}
        onFinish={save}
        onValuesChange={() => setDirty(true)}
        className="grid grid-cols-1 gap-x-4 md:grid-cols-2"
      >
        <LeadFormFields excludeId={lead.id} onOpenLead={onOpenLead} />
        <div className="sticky bottom-0 -mx-6 flex flex-col-reverse gap-2 border-t border-line bg-surface px-6 py-3 sm:flex-row sm:justify-end md:col-span-2">
          <Button
            action="reset"
            disabled={!dirty || saving}
            onClick={() => {
              form.resetFields();
              setDirty(false);
            }}
          >
            Hoàn tác thay đổi
          </Button>
          <Button action="save" htmlType="submit" loading={saving} disabled={!dirty}>
            Lưu thay đổi
          </Button>
        </div>
      </Form>
    </div>
  );
}
