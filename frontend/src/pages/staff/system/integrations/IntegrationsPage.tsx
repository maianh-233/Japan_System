import { useState } from 'react';
import { Drawer, Form, Switch } from 'antd';
import { CheckCircle2, Copy, FlaskConical, Plug, PlugZap, RefreshCw, Settings2, TriangleAlert, Unplug, XCircle } from 'lucide-react';
import { Button, ModalForm, PageHeader, StatCard, StatusTag, TextInput, useConfirm, useNotify } from '@/components/common';
import { INTEGRATION_CATEGORIES, INTEGRATION_STATUS } from '@/features/system/constants';
import { useSystemStore } from '@/features/system/systemStore';
import type { Integration, IntegrationCategory } from '@/features/system/types';
import { useActor } from '@/features/system/useActor';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDateTime, fromNow } from '@/lib/date';
import { formatNumber } from '@/lib/format';

const mask = (v?: string) => (v ? `••••••${v.slice(-4)}` : '—');

/** Tích hợp: Zalo OA, SMS, Email, VietQR / webhook ngân hàng, Zoom / Meet, nguồn lead. DỮ LIỆU GIẢ — không gọi dịch vụ thật. */
export default function IntegrationsPage() {
  const { can } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const { actor } = useActor();
  const { integrations, intLogs, updateIntegration, addIntLog } = useSystemStore();
  const editable = can('system.integrations');

  const [openId, setOpenId] = useState<string | null>(null);
  const [configuring, setConfiguring] = useState<Integration | null>(null);
  const [testing, setTesting] = useState<string | null>(null);
  const open = integrations.find((x) => x.id === openId);

  const test = async (it: Integration) => {
    setTesting(it.id);
    await new Promise((r) => setTimeout(r, 900)); // giả lập gọi dịch vụ
    setTesting(null);
    const ok = it.status === 'connected';
    addIntLog({ integrationId: it.id, ok, message: ok ? `Kiểm tra kết nối thành công (${actor.name})` : `Kiểm tra thất bại: ${it.error ?? 'chưa kết nối'}` });
    if (ok) {
      updateIntegration(it.id, { lastSyncAt: new Date().toISOString() }, actor, 'Kiểm tra tích hợp');
      notify.success({ title: `${it.name}: kết nối tốt`, description: 'Đã gửi tin / gọi thử thành công.' });
    } else notify.error({ title: `${it.name}: kiểm tra thất bại`, description: it.error ?? 'Chưa kết nối.' });
  };

  const disconnect = async (it: Integration) => {
    const ok = await confirm({
      title: `Ngắt kết nối ${it.name}?`,
      tone: 'danger',
      consequences: it.events.filter((e) => e.on).map((e) => `Dừng: ${e.label}`),
      confirmText: 'Ngắt kết nối',
    });
    if (!ok) return;
    updateIntegration(it.id, { status: 'disconnected', account: undefined, config: {}, error: undefined, events: it.events.map((e) => ({ ...e, on: false })) }, actor, 'Ngắt tích hợp');
    notify.toast.success(`Đã ngắt ${it.name}`);
  };

  const errors = integrations.filter((x) => x.status === 'error');
  const failed = integrations.reduce((s, x) => s + (x.usage?.failed ?? 0), 0);

  return (
    <>
      <PageHeader
        title="Tích hợp"
        subtitle="Kết nối dịch vụ bên ngoài. Khóa bí mật được mã hóa, chỉ hiện 4 ký tự cuối. Mọi thay đổi ghi vào Nhật ký hệ thống."
        breadcrumb={[{ title: 'Hệ thống' }, { title: 'Tích hợp' }]}
      />

      {errors.length > 0 && (
        <div className="mb-4 flex flex-col gap-2">
          {errors.map((x) => (
            <div key={x.id} className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-danger-100 bg-danger-50 px-4 py-3 text-[13px] text-danger-700">
              <span className="flex items-start gap-2">
                <TriangleAlert size={16} className="mt-0.5 shrink-0" aria-hidden />
                <span>
                  <b>{x.name}:</b> {x.error}
                </span>
              </span>
              {editable && (
                <Button icon={RefreshCw} size="sm" tone="danger" variant="soft" onClick={() => setConfiguring(x)}>
                  Kết nối lại
                </Button>
              )}
            </div>
          ))}
        </div>
      )}

      <div className="mb-5 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Đang kết nối" value={`${integrations.filter((x) => x.status === 'connected').length}/${integrations.length}`} icon={Plug} tone="success" />
        <StatCard label="Lỗi" value={errors.length} icon={TriangleAlert} tone={errors.length ? 'danger' : 'neutral'} />
        <StatCard label="Tin đã gửi (30 ngày)" value={formatNumber(integrations.filter((x) => x.category === 'notify').reduce((s, x) => s + (x.usage?.value ?? 0), 0))} icon={PlugZap} tone="primary" hint="Zalo + SMS + Email" />
        <StatCard label="Lượt thất bại (30 ngày)" value={failed} icon={XCircle} tone="warning" />
      </div>

      {(Object.keys(INTEGRATION_CATEGORIES) as IntegrationCategory[]).map((cat) => {
        const C = INTEGRATION_CATEGORIES[cat];
        const list = integrations.filter((x) => x.category === cat);
        return (
          <section key={cat} className="mb-5">
            <h2 className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-ink">
              <C.icon size={16} aria-hidden /> {C.label}
            </h2>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {list.map((x) => {
                const S = INTEGRATION_STATUS[x.status];
                return (
                  <article key={x.id} className={cn('flex flex-col rounded-xl border bg-surface p-4', x.status === 'error' ? 'border-danger-100' : 'border-line')}>
                    <div className="flex items-start justify-between gap-2">
                      <h3 className="font-semibold text-ink">{x.name}</h3>
                      <StatusTag tone={S.tone} icon={S.icon} size="sm">
                        {S.label}
                      </StatusTag>
                    </div>
                    <p className="mt-1 text-[13px] text-ink-secondary">{x.description}</p>
                    {x.status !== 'disconnected' && (
                      <div className="mt-3 rounded-lg bg-canvas px-3 py-2 text-xs text-ink-secondary">
                        <p className="truncate">
                          <span className="text-ink-muted">Tài khoản:</span> {x.account}
                        </p>
                        <p>
                          <span className="text-ink-muted">Đồng bộ:</span> {x.lastSyncAt ? fromNow(x.lastSyncAt) : '—'}
                        </p>
                        {x.usage && (
                          <p>
                            <span className="tabular font-semibold text-ink">{formatNumber(x.usage.value)}</span> {x.usage.label} / 30 ngày
                            {!!x.usage.failed && <span className="text-danger-700"> · {x.usage.failed} lỗi</span>}
                          </p>
                        )}
                      </div>
                    )}
                    <div className="mt-auto flex flex-wrap gap-1.5 pt-3">
                      {x.status === 'disconnected' ? (
                        editable && (
                          <Button icon={PlugZap} size="sm" onClick={() => setConfiguring(x)}>
                            Kết nối
                          </Button>
                        )
                      ) : (
                        <>
                          <Button icon={Settings2} size="sm" variant="soft" onClick={() => setOpenId(x.id)}>
                            Chi tiết
                          </Button>
                          <Button icon={FlaskConical} size="sm" variant="ghost" tone="neutral" loading={testing === x.id} onClick={() => test(x)}>
                            Kiểm tra
                          </Button>
                        </>
                      )}
                    </div>
                  </article>
                );
              })}
            </div>
          </section>
        );
      })}

      {/* ===== Chi tiết ===== */}
      <Drawer open={Boolean(open)} onClose={() => setOpenId(null)} width={isMobile ? '100%' : 560} destroyOnHidden title={open?.name}>
        {open && (
          <div className="flex flex-col gap-4 text-[13px]">
            <div className="flex flex-wrap items-center gap-2 text-ink-secondary">
              <StatusTag tone={INTEGRATION_STATUS[open.status].tone} icon={INTEGRATION_STATUS[open.status].icon} size="sm">
                {INTEGRATION_STATUS[open.status].label}
              </StatusTag>
              {open.connectedAt && (
                <span>
                  Kết nối {fmtDateTime(open.connectedAt)} bởi {open.connectedBy}
                </span>
              )}
            </div>

            <div className="rounded-xl border border-line p-3">
              <p className="mb-2 font-semibold text-ink">Cấu hình</p>
              <dl className="grid grid-cols-[130px_1fr] gap-x-3 gap-y-1.5">
                {open.fields.map((f) => (
                  <div key={f.key} className="contents">
                    <dt className="text-ink-muted">{f.label}</dt>
                    <dd className="tabular truncate">{f.secret ? mask(open.config[f.key]) : (open.config[f.key] ?? '—')}</dd>
                  </div>
                ))}
              </dl>
              {editable && (
                <Button action="edit" size="sm" className="mt-3" onClick={() => setConfiguring(open)}>
                  Sửa cấu hình
                </Button>
              )}
            </div>

            {open.webhook && (
              <div className="rounded-xl border border-line p-3">
                <p className="mb-1 font-semibold text-ink">Webhook nhận dữ liệu</p>
                <p className="mb-2 text-xs text-ink-muted">Dán URL này vào trang quản trị của dịch vụ. Tạo lại URL nếu nghi bị lộ — URL cũ ngừng hoạt động ngay.</p>
                <div className="flex items-center gap-2">
                  <code className="min-w-0 flex-1 truncate rounded bg-canvas px-2 py-1.5">{open.webhook}</code>
                  <Button
                    icon={Copy}
                    iconOnly
                    size="sm"
                    variant="soft"
                    aria-label="Sao chép"
                    onClick={() => {
                      void navigator.clipboard?.writeText(open.webhook!);
                      notify.toast.success('Đã sao chép URL');
                    }}
                  />
                  {editable && (
                    <Button
                      icon={RefreshCw}
                      iconOnly
                      size="sm"
                      variant="ghost"
                      tone="neutral"
                      aria-label="Tạo lại URL"
                      onClick={async () => {
                        const ok = await confirm({ title: 'Tạo lại URL webhook?', tone: 'warning', description: 'URL cũ ngừng nhận dữ liệu ngay. Nhớ cập nhật bên dịch vụ.' });
                        if (ok) updateIntegration(open.id, { webhook: open.webhook!.replace(/[^/]+$/, Math.random().toString(16).slice(2, 8)) }, actor, 'Tạo lại webhook');
                      }}
                    />
                  )}
                </div>
              </div>
            )}

            <div className="rounded-xl border border-line p-3">
              <p className="mb-2 font-semibold text-ink">Dùng cho</p>
              <ul className="flex flex-col gap-2">
                {open.events.map((e) => (
                  <li key={e.key} className="flex items-center justify-between gap-3">
                    <span>{e.label}</span>
                    <Switch
                      size="small"
                      checked={e.on}
                      disabled={!editable || open.status === 'disconnected'}
                      onChange={(v) => updateIntegration(open.id, { events: open.events.map((x) => (x.key === e.key ? { ...x, on: v } : x)) }, actor, `${v ? 'Bật' : 'Tắt'} “${e.label}”`)}
                      aria-label={e.label}
                    />
                  </li>
                ))}
              </ul>
            </div>

            <div>
              <p className="mb-2 font-semibold text-ink">Nhật ký gần đây</p>
              <ul className="flex flex-col gap-1.5">
                {intLogs
                  .filter((l) => l.integrationId === open.id)
                  .slice(0, 12)
                  .map((l) => (
                    <li key={l.id} className="flex items-start gap-2">
                      {l.ok ? <CheckCircle2 size={14} className="mt-0.5 shrink-0 text-success-600" aria-label="Thành công" /> : <XCircle size={14} className="mt-0.5 shrink-0 text-danger-600" aria-label="Lỗi" />}
                      <span className="min-w-0 flex-1">
                        {l.message}
                        <span className="block text-[11px] text-ink-muted">{fmtDateTime(l.at)}</span>
                      </span>
                    </li>
                  ))}
                {!intLogs.some((l) => l.integrationId === open.id) && <li className="text-ink-muted">Chưa có hoạt động.</li>}
              </ul>
            </div>

            {editable && (
              <Button icon={Unplug} variant="ghost" tone="danger" className="self-start" onClick={() => disconnect(open)}>
                Ngắt kết nối
              </Button>
            )}
          </div>
        )}
      </Drawer>

      {/* ===== Kết nối / sửa cấu hình ===== */}
      <ModalForm<Record<string, string>>
        key={configuring?.id ?? 'cfg'}
        open={Boolean(configuring)}
        onClose={() => setConfiguring(null)}
        title={configuring ? `${configuring.status === 'disconnected' ? 'Kết nối' : 'Cấu hình'} ${configuring.name}` : ''}
        description="Khóa bí mật: để trống = giữ khóa cũ."
        submitAction="save"
        submitText="Lưu & kiểm tra"
        successMessage="Đã kết nối"
        initialValues={configuring ? Object.fromEntries(configuring.fields.filter((f) => !f.secret).map((f) => [f.key, configuring.config[f.key] ?? ''])) : {}}
        onSubmit={async (v) => {
          const it = configuring!;
          const missing = it.fields.filter((f) => !v[f.key] && !it.config[f.key]);
          if (missing.length) throw new Error(`Thiếu: ${missing.map((f) => f.label).join(', ')}`);
          await new Promise((r) => setTimeout(r, 800)); // giả lập kiểm tra kết nối
          const config = { ...it.config, ...Object.fromEntries(Object.entries(v).filter(([, x]) => x)) };
          const first = it.fields.find((f) => !f.secret)?.key;
          updateIntegration(
            it.id,
            {
              config,
              status: 'connected',
              error: undefined,
              account: it.account ?? (first ? config[first] : it.name),
              connectedAt: new Date().toISOString(),
              connectedBy: actor.name,
              lastSyncAt: new Date().toISOString(),
              events: it.status === 'disconnected' ? it.events.map((e) => ({ ...e, on: true })) : it.events,
            },
            actor,
            it.status === 'disconnected' ? 'Kết nối tích hợp' : 'Sửa cấu hình tích hợp',
          );
          addIntLog({ integrationId: it.id, ok: true, message: `Kết nối thành công (${actor.name})` });
        }}
      >
        {configuring?.fields.map((f) => (
          <Form.Item key={f.key} name={f.key} label={f.label} extra={f.secret && configuring.config[f.key] ? `Hiện tại: ${mask(configuring.config[f.key])}` : undefined}>
            <TextInput type={f.secret ? 'password' : 'text'} placeholder={f.placeholder} autoComplete="off" />
          </Form.Item>
        ))}
      </ModalForm>
      {confirmDialog}
    </>
  );
}
