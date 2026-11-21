/**
 * Nối Mẫu tin nhắn + gửi tin với backend thật (/api/v1/crm/message-templates, /messages — CRM-17).
 * ⏳ Gửi Zalo / SMS thật làm cùng Tích hợp (giai đoạn 22): tin được lưu nhật ký ở trạng thái "chờ gửi".
 */
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useMessageStore, type MessageChannel, type MessageLog, type MessageScenario, type MessageTemplate } from './messages';

interface ServerTemplate {
  id: string;
  scenario: MessageScenario;
  name: string;
  channel: MessageChannel;
  body: string;
  active: boolean;
  updatedAt: string;
  version: number;
}

interface ServerLog {
  id: string;
  channel: MessageChannel;
  scenario: MessageScenario;
  templateId?: string | null;
  toName: string;
  toPhone: string;
  leadId?: string | null;
  studentId?: string | null;
  body: string;
  sentBy: string;
  sentAt: string;
  auto: boolean;
  status: 'pending' | 'sent' | 'failed';
}

const versions = new Map<string, number>();

const toTemplate = (t: ServerTemplate): MessageTemplate => {
  const id = fromGuid(t.id);
  versions.set(id, t.version);
  return { id, scenario: t.scenario, name: t.name, channel: t.channel, body: t.body, active: t.active, updatedAt: t.updatedAt, updatedBy: '' };
};

const toLog = (l: ServerLog): MessageLog => ({
  id: fromGuid(l.id),
  channel: l.channel,
  scenario: l.scenario,
  templateId: l.templateId ? fromGuid(l.templateId) : undefined,
  toName: l.toName,
  toPhone: l.toPhone,
  leadId: l.leadId ? fromGuid(l.leadId) : undefined,
  studentId: l.studentId ? fromGuid(l.studentId) : undefined,
  body: l.body,
  sentBy: l.sentBy,
  sentAt: l.sentAt,
  auto: l.auto,
});

export async function loadTemplates() {
  try {
    useMessageStore.setState({ templates: (await api<ServerTemplate[]>('/crm/message-templates')).map(toTemplate) });
  } catch (e) {
    if (!(e instanceof ApiError) || e.status !== 403) throw e;
  }
}

export async function saveTemplate(id: string | undefined, v: Pick<MessageTemplate, 'scenario' | 'name' | 'channel' | 'body' | 'active'>) {
  const t = toTemplate(
    await api<ServerTemplate>(id ? `/crm/message-templates/${toGuid(id)}` : '/crm/message-templates', {
      method: id ? 'PUT' : 'POST',
      body: { ...v, version: id ? (versions.get(id) ?? 0) : 0 },
    }),
  );
  useMessageStore.setState((s) => ({
    templates: s.templates.some((x) => x.id === t.id) ? s.templates.map((x) => (x.id === t.id ? t : x)) : [...s.templates, t],
  }));
  return t;
}

export async function deleteTemplate(id: string) {
  await api(`/crm/message-templates/${toGuid(id)}`, { method: 'DELETE' });
  useMessageStore.setState((s) => ({ templates: s.templates.filter((x) => x.id !== id) }));
}

/** Tin đã gửi cho 1 lead hoặc 1 học viên (thay phần tương ứng trong store) */
export async function loadMessages(target: { leadId?: string; studentId?: string }) {
  const q = target.leadId ? `leadId=${toGuid(target.leadId)}` : `studentId=${toGuid(target.studentId!)}`;
  const logs = (await api<ServerLog[]>(`/crm/messages?${q}`)).map(toLog);
  useMessageStore.setState((s) => ({
    logs: [...logs, ...s.logs.filter((l) => (target.leadId ? l.leadId !== target.leadId : l.studentId !== target.studentId))],
  }));
}

export async function sendMessage(m: Omit<MessageLog, 'id' | 'sentAt' | 'sentBy' | 'auto'>) {
  const log = toLog(
    await api<ServerLog>('/crm/messages', {
      body: {
        channel: m.channel,
        scenario: m.scenario,
        templateId: m.templateId ? toGuid(m.templateId) : null,
        leadId: m.leadId ? toGuid(m.leadId) : null,
        studentId: m.studentId ? toGuid(m.studentId) : null,
        toName: m.toName,
        toPhone: m.toPhone,
        body: m.body,
      },
    }),
  );
  useMessageStore.setState((s) => ({ logs: [log, ...s.logs] }));
  return log;
}
