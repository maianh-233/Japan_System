/**
 * Nối Cài đặt chung với backend (/api/v1/system/settings — user story 10, CD-01 / CD-02).
 * Tải 1 lần sau đăng nhập (nhân viên, GV) đổ vào useSettingsStore — mọi chỗ đọc getSettings() / useSettings() dùng giá trị thật.
 * Lưu: gửi cả bộ; server kiểm tra, chỉ ghi mục đổi và tự ghi Nhật ký hệ thống (trước → sau).
 */
import { api } from '@/lib/api/http';
import { useSettingsStore, type CenterSettings } from './settings';

interface ServerSettings {
  /** Cùng hình dạng CenterSettings; `office.satTo` = null khi nghỉ thứ 7 */
  settings: CenterSettings & { office: CenterSettings['office'] & { satTo?: string | null } };
  updatedAt?: string | null;
  updatedBy?: string | null;
  changed?: number | null;
}

function apply(r: ServerSettings) {
  const { satTo, ...office } = r.settings.office;
  useSettingsStore.setState({
    settings: { ...r.settings, office: { ...office, ...(satTo ? { satTo } : {}) } },
    updatedAt: r.updatedAt ?? undefined,
    updatedBy: r.updatedBy ?? undefined,
  });
}

export async function loadSettings() {
  apply(await api<ServerSettings>('/system/settings'));
}

/** Lưu cả bộ — trả số mục đã đổi. Sai giá trị → ApiError (lỗi theo từng mục "nhóm.mục" trong `fields`). */
export async function saveSettings(next: CenterSettings): Promise<number> {
  const r = await api<ServerSettings>('/system/settings', {
    method: 'PUT',
    body: { settings: { ...next, office: { ...next.office, satTo: next.office.satTo ?? null } } },
  });
  apply(r);
  return r.changed ?? 0;
}
