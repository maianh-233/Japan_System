/**
 * Hệ thống › Tích hợp (user story 15, giai đoạn 22) — backend System: /api/v1/system/integrations.
 * Đổ vào `useSystemStore.integrations` / `intLogs` như dữ liệu giả cũ. Khóa bí mật server đã che (`••••••abcd`) — trang hiển thị nguyên.
 * Kết nối / ngắt / bật việc / kiểm tra gọi API rồi thay bản server; nhật ký (50 dòng mới nhất) tải khi mở chi tiết.
 */
import { USE_API } from '@/lib/api/config';
import { api } from '@/lib/api/http';
import { useSystemStore } from './systemStore';
import type { Integration, IntegrationCategory, IntegrationLog, IntegrationStatus } from './types';

interface ServerIntegration {
  id: string;
  name: string;
  category: IntegrationCategory;
  description: string;
  status: IntegrationStatus;
  account?: string | null;
  connectedAt?: string | null;
  connectedBy?: string | null;
  lastSyncAt?: string | null;
  error?: string | null;
  fields: { key: string; label: string; secret: boolean; required: boolean }[];
  config: Record<string, string>;
  events: { key: string; label: string; on: boolean }[];
  usage: { label: string; value: number; failed: number };
  webhook?: string | null;
}

interface ServerLog {
  id: string;
  integrationId: string;
  at: string;
  ok: boolean;
  message: string;
}

const toIntegration = (i: ServerIntegration): Integration => ({
  id: i.id,
  name: i.name,
  category: i.category,
  description: i.description,
  status: i.status,
  account: i.account ?? undefined,
  connectedAt: i.connectedAt ?? undefined,
  connectedBy: i.connectedBy ?? undefined,
  lastSyncAt: i.lastSyncAt ?? undefined,
  error: i.error ?? undefined,
  fields: i.fields.map((f) => ({ key: f.key, label: f.label, secret: f.secret || undefined })),
  config: i.config,
  events: i.events,
  usage: i.usage,
  webhook: i.webhook ?? undefined,
});

const toLog = (l: ServerLog): IntegrationLog => ({ id: l.id, integrationId: l.integrationId, at: l.at, ok: l.ok, message: l.message });

function upsert(i: ServerIntegration) {
  const it = toIntegration(i);
  useSystemStore.setState((s) => ({ integrations: s.integrations.map((x) => (x.id === it.id ? it : x)) }));
  return it;
}

const path = (key: string) => `/system/integrations/${encodeURIComponent(key)}`;

/** TH-01 — danh sách + lượt dùng 30 ngày */
export async function loadIntegrations() {
  const list = await api<ServerIntegration[]>('/system/integrations');
  useSystemStore.setState({ integrations: list.map(toIntegration), intLogs: [] });
}

/** TH-05 — 50 dòng nhật ký mới nhất của 1 tích hợp */
export async function loadIntegrationLogs(key: string) {
  const logs = await api<ServerLog[]>(`${path(key)}/logs`);
  useSystemStore.setState((s) => ({ intLogs: [...s.intLogs.filter((l) => l.integrationId !== key), ...logs.map(toLog)] }));
}

/** TH-02 — kết nối / kết nối lại; trường bí mật để trống = giữ khóa cũ */
export async function connectIntegration(key: string, config: Record<string, string>, account?: string) {
  return upsert(await api<ServerIntegration>(`${path(key)}/connection`, { method: 'PUT', body: { config, account: account ?? null } }));
}

/** TH-02 — ngắt: server xóa cấu hình, tắt hết việc */
export async function disconnectIntegration(key: string) {
  return upsert(await api<ServerIntegration>(`${path(key)}/connection`, { method: 'DELETE' }));
}

/** TH-03 — bật / tắt 1 việc */
export async function setIntegrationEvent(key: string, event: string, on: boolean) {
  return upsert(await api<ServerIntegration>(`${path(key)}/events/${encodeURIComponent(event)}`, { method: 'PUT', body: { on } }));
}

/** TH-04 — gọi thử dịch vụ; ghi nhật ký, lỗi xác thực → trạng thái lỗi */
export async function testIntegration(key: string) {
  const r = await api<{ ok: boolean; message: string; integration: ServerIntegration }>(`${path(key)}/test`, { method: 'POST' });
  upsert(r.integration);
  await loadIntegrationLogs(key).catch(() => undefined);
  return { ok: r.ok, message: r.message };
}

// Chế độ API: không lẫn dữ liệu giả trước khi tải
if (USE_API) useSystemStore.setState({ integrations: [], intLogs: [] });
