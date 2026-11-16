import { useMemo, useRef, useState } from 'react';
import { Form, Radio, Segmented, Switch } from 'antd';
import dayjs from 'dayjs';
import { Bot, MessageSquareText } from 'lucide-react';
import { Button, DataTable, ModalForm, PageHeader, Select, StatusTag, TextArea, TextInput, useConfirm, type DataColumn } from '@/components/common';
import {
  CHANNEL_LABEL,
  fillTemplate,
  MESSAGE_VARS,
  SCENARIO_OPTIONS,
  SCENARIOS,
  smsSegments,
  useMessageStore,
  type MessageChannel,
  type MessageLog,
  type MessageScenario,
  type MessageTemplate,
} from '@/features/crm/messages/messages';
import { useAuth } from '@/hooks/useAuth';
import { fmtDateTime } from '@/lib/date';

interface Values {
  scenario: MessageScenario;
  name: string;
  channel: MessageChannel;
  body: string;
  active: boolean;
}

const EXAMPLE = Object.fromEntries(MESSAGE_VARS.map((v) => [v.key, v.example]));

/** Mẫu tin nhắn Zalo / SMS: chào lead mới, nhắc lịch test, nhắc học phí, báo nghỉ học... + nhật ký đã gửi */
export default function MessageTemplatesPage() {
  const { user, can } = useAuth();
  const { confirm, confirmDialog } = useConfirm();
  const templates = useMessageStore((s) => s.templates);
  const logs = useMessageStore((s) => s.logs);
  const save = useMessageStore((s) => s.saveTemplate);
  const remove = useMessageStore((s) => s.removeTemplate);
  const editable = can('crm.message-templates');
  const [tab, setTab] = useState<'templates' | 'logs'>('templates');
  const [scenario, setScenario] = useState<MessageScenario>();
  const [editing, setEditing] = useState<MessageTemplate | 'new' | null>(null);

  const data = useMemo(() => templates.filter((t) => !scenario || t.scenario === scenario), [templates, scenario]);

  const columns: DataColumn<MessageTemplate>[] = [
    {
      key: 'name',
      title: 'Mẫu',
      width: 240,
      mobile: 'title',
      render: (_, t) => (
        <span className="flex flex-col">
          <span className="font-medium text-ink">{t.name}</span>
          <span className="text-xs text-ink-muted">{SCENARIOS[t.scenario].label}</span>
        </span>
      ),
    },
    { key: 'channel', title: 'Kênh', width: 80, render: (_, t) => CHANNEL_LABEL[t.channel] },
    { key: 'body', title: 'Nội dung', width: 420, ellipsis: false, render: (_, t) => <span className="line-clamp-2 text-[13px] text-ink-secondary">{t.body}</span> },
    {
      key: 'active',
      title: 'Đang dùng',
      width: 100,
      mobile: 'aside',
      render: (_, t) => <Switch size="small" checked={t.active} disabled={!editable} onChange={(v) => save({ ...t, active: v, updatedBy: user?.fullName ?? '' })} />,
    },
    { key: 'updated', title: 'Cập nhật', width: 150, render: (_, t) => <span className="text-xs text-ink-muted">{t.updatedBy} · {dayjs(t.updatedAt).format('DD/MM/YYYY')}</span> },
  ];

  const logColumns: DataColumn<MessageLog>[] = [
    { key: 'sentAt', title: 'Thời gian', width: 140, render: (_, l) => <span className="tabular">{fmtDateTime(l.sentAt)}</span> },
    { key: 'to', title: 'Người nhận', width: 200, mobile: 'title', render: (_, l) => <span className="flex flex-col"><span className="font-medium text-ink">{l.toName}</span><span className="text-xs text-ink-muted">{l.toPhone}</span></span> },
    { key: 'scenario', title: 'Tình huống', width: 140, render: (_, l) => SCENARIOS[l.scenario].label },
    { key: 'channel', title: 'Kênh', width: 70, render: (_, l) => CHANNEL_LABEL[l.channel] },
    { key: 'body', title: 'Nội dung', width: 380, ellipsis: false, render: (_, l) => <span className="line-clamp-2 text-[13px] text-ink-secondary" title={l.body}>{l.body}</span> },
    {
      key: 'by',
      title: 'Người gửi',
      width: 150,
      mobile: 'aside',
      render: (_, l) =>
        l.auto ? (
          <StatusTag tone="info" icon={Bot} size="sm">
            Tự động
          </StatusTag>
        ) : (
          l.sentBy
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Mẫu tin nhắn"
        subtitle="Zalo / SMS soạn sẵn — tự điền tên, lớp, số tiền, lịch hẹn khi gửi từ hồ sơ lead / học viên"
        breadcrumb={[{ title: 'Tuyển sinh' }, { title: 'Mẫu tin nhắn' }]}
        actions={
          editable && (
            <Button action="create" collapseOnMobile onClick={() => setEditing('new')}>
              Thêm mẫu
            </Button>
          )
        }
      />
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <Segmented
          value={tab}
          onChange={(v) => setTab(v as typeof tab)}
          options={[
            { value: 'templates', label: `Mẫu (${templates.length})` },
            { value: 'logs', label: `Đã gửi (${logs.length})` },
          ]}
        />
        {tab === 'templates' && <Select className="!w-48" placeholder="Mọi tình huống" value={scenario} onChange={(v) => setScenario(v as MessageScenario | undefined)} options={SCENARIO_OPTIONS} />}
      </div>

      {tab === 'templates' ? (
        <DataTable<MessageTemplate>
          storageKey="crm-msg-templates"
          columns={columns}
          data={data}
          rowKey="id"
          unitLabel="mẫu"
          searchKeys={['name', 'body']}
          onRowClick={editable ? (t) => setEditing(t) : undefined}
          rowActions={
            editable
              ? (t) => (
                  <>
                    <Button action="edit" iconOnly size="sm" onClick={() => setEditing(t)} />
                    <Button
                      action="delete"
                      iconOnly
                      size="sm"
                      onClick={async () => {
                        if (await confirm({ title: `Xóa mẫu "${t.name}"?`, description: 'Tin đã gửi trước đây không bị ảnh hưởng.', tone: 'danger' })) remove(t.id);
                      }}
                    />
                  </>
                )
              : undefined
          }
          actionsWidth={100}
          empty={{ icon: MessageSquareText, title: 'Chưa có mẫu', description: 'Thêm mẫu cho các tình huống gửi thường xuyên.' }}
        />
      ) : (
        <DataTable<MessageLog>
          storageKey="crm-msg-logs"
          columns={logColumns}
          data={logs}
          rowKey="id"
          unitLabel="tin"
          searchKeys={['toName', 'toPhone', 'body']}
          empty={{ icon: MessageSquareText, title: 'Chưa gửi tin nào', description: 'Gửi từ hồ sơ lead / học viên, hoặc tin nhắc lịch hẹn tự động trước 1 giờ.' }}
        />
      )}

      <TemplateDialog editing={editing} onClose={() => setEditing(null)} onSave={(v, id) => save({ ...v, id, updatedBy: user?.fullName ?? 'Nhân viên' })} />
      {confirmDialog}
    </>
  );
}

function TemplateDialog({ editing, onClose, onSave }: { editing: MessageTemplate | 'new' | null; onClose: () => void; onSave: (v: Values, id?: string) => void }) {
  const [form] = Form.useForm<Values>();
  const body = Form.useWatch('body', form) ?? '';
  const channel = Form.useWatch('channel', form);
  const sc = Form.useWatch('scenario', form) as MessageScenario | undefined;
  const cursor = useRef<number | null>(null);
  const t = editing && editing !== 'new' ? editing : undefined;

  const insert = (key: string) => {
    const pos = cursor.current ?? body.length;
    form.setFieldsValue({ body: `${body.slice(0, pos)}{${key}}${body.slice(pos)}` });
    cursor.current = pos + key.length + 2;
  };
  const preview = fillTemplate(body, EXAMPLE);
  const sms = smsSegments(preview);
  const vars = MESSAGE_VARS.filter((v) => !sc || SCENARIOS[sc].audience.some((a) => v.for.includes(a)));

  return (
    <ModalForm<Values>
      key={t?.id ?? 'new'}
      form={form}
      open={Boolean(editing)}
      onClose={onClose}
      width={720}
      title={t ? 'Sửa mẫu tin nhắn' : 'Thêm mẫu tin nhắn'}
      submitAction="save"
      successMessage="Đã lưu mẫu"
      confirmOnDiscard
      initialValues={t ? { scenario: t.scenario, name: t.name, channel: t.channel, body: t.body, active: t.active } : { scenario: 'custom', channel: 'zalo', active: true, body: '' }}
      onSubmit={(v) => onSave({ ...v, name: v.name.trim(), body: v.body.trim() }, t?.id)}
    >
      <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
        <Form.Item name="scenario" label="Tình huống" rules={[{ required: true }]} extra={sc && SCENARIOS[sc].hint}>
          <Select options={SCENARIO_OPTIONS} allowClear={false} showSearch={false} />
        </Form.Item>
        <Form.Item name="name" label="Tên mẫu" rules={[{ required: true, whitespace: true, message: 'Nhập tên mẫu' }]}>
          <TextInput maxLength={60} placeholder="VD: Nhắc học phí lần 2" />
        </Form.Item>
      </div>
      <div className="flex flex-wrap items-center gap-x-6">
        <Form.Item name="channel" label="Kênh gợi ý">
          <Radio.Group optionType="button" options={[{ value: 'zalo', label: 'Zalo' }, { value: 'sms', label: 'SMS' }]} />
        </Form.Item>
        <Form.Item name="active" label="Đang dùng" valuePropName="checked">
          <Switch />
        </Form.Item>
      </div>
      <Form.Item name="body" label="Nội dung" rules={[{ required: true, whitespace: true, message: 'Nhập nội dung' }]}>
        <TextArea rows={5} maxLength={1000} showCount onSelect={(e) => (cursor.current = (e.target as HTMLTextAreaElement).selectionStart)} onBlur={(e) => (cursor.current = e.target.selectionStart)} />
      </Form.Item>
      <div className="-mt-2 mb-4">
        <p className="mb-1.5 text-xs text-ink-muted">Bấm để chèn thông tin tự điền vào vị trí con trỏ:</p>
        <div className="flex flex-wrap gap-1.5">
          {vars.map((v) => (
            <button key={v.key} type="button" onClick={() => insert(v.key)} title={`VD: ${v.example}`} className="rounded-full border border-line bg-page px-2.5 py-0.5 text-xs text-ink-secondary hover:border-primary-300 hover:text-primary-700">
              {v.label}
            </button>
          ))}
        </div>
      </div>
      <div className="rounded-lg bg-page p-3">
        <p className="mb-1 text-xs font-medium text-ink-muted">Xem trước với dữ liệu ví dụ</p>
        <p className="whitespace-pre-line text-[13px] text-ink">{preview || '—'}</p>
        {channel === 'sms' && <p className="mt-2 text-xs text-ink-muted">{sms.chars} ký tự · {sms.segments} tin SMS{sms.unicode ? ' · có dấu (70 ký tự / tin) — nên viết không dấu' : ''}</p>}
      </div>
    </ModalForm>
  );
}
