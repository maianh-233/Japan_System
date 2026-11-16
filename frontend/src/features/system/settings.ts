import dayjs from 'dayjs';
import { create } from 'zustand';
import type { AuditLog } from './types';

/**
 * CÀI ĐẶT CHUNG CỦA TRUNG TÂM — trước đây nằm rải rác trong code (SLA_MINUTES, OFFICE_HOURS, ALERT_RULES, mã hóa đơn...).
 * Đổi ở trang Hệ thống › Cài đặt chung, mọi thay đổi ghi nhật ký (trước → sau).
 * Code đọc qua `getSettings()` (ngoài React) hoặc `useSettings()` (trong component, tự cập nhật).
 * Khi có backend: tải 1 lần lúc đăng nhập, lưu bằng API PUT /settings.
 */

export interface CenterSettings {
  crm: {
    /** SLA liên hệ lead mới (phút) khi không có quy tắc chia lead nào khớp */
    defaultSlaMinutes: number;
  };
  office: {
    /** Giờ hành chính T2–T6 */
    from: string;
    to: string;
    /** Thứ 7 làm đến (bỏ trống = nghỉ thứ 7) */
    satTo?: string;
    /** Đi muộn / về sớm quá N phút mới tính vi phạm */
    lateGraceMin: number;
  };
  invoice: {
    /** Mẫu mã hóa đơn: {CN} tiền tố chi nhánh · {YY} {MM} năm / tháng · {SO:n} số thứ tự n chữ số */
    codePattern: string;
    /** Đánh số lại từ 1 mỗi: năm / tháng / không bao giờ */
    resetEvery: 'never' | 'year' | 'month';
  };
  payroll: {
    /** Ngày trong tháng SAU phải chốt bảng lương tháng trước (quá ngày này chưa chốt → cảnh báo) */
    lockDay: number;
  };
  atRisk: {
    /** Vắng không phép từ N buổi */
    absent: number;
    /** Vắng liên tiếp từ N buổi */
    streak: number;
    /** Chuyên cần dưới N% */
    attendanceRate: number;
    /** Tỉ lệ nộp bài dưới N% */
    submitRate: number;
    /** Điểm dưới N% */
    scorePct: number;
    /** Tổng điểm rủi ro (dấu hiệu nặng = 3, nhẹ = 1) từ N → mức Trung bình / Cao */
    mediumFrom: number;
    highFrom: number;
  };
  parent: {
    /** Báo phụ huynh khi con được điểm danh có mặt / đi muộn */
    arrivalNotice: boolean;
    /** Quá N phút sau giờ vào lớp chưa có mặt (và không có đơn nghỉ) → báo phụ huynh. 0 = tắt */
    absentAlertMin: number;
    /** Chỉ báo đến lớp / vắng cho học viên dưới N tuổi (0 = mọi học viên có phụ huynh) */
    underAge: number;
    /** Tự gửi báo cáo tuần */
    weeklyReport: boolean;
    /** Ngày gửi trong tuần: 0 = Chủ nhật … 6 = Thứ 7 */
    weeklyDay: number;
    /** Giờ gửi HH:mm */
    weeklyTime: string;
  };
  safety: {
    /** Hoàn tiền từ số tiền này trở lên cần người thứ 2 duyệt (0 = mọi khoản) */
    refundApprovalMin: number;
    /** Số ngày giữ dữ liệu trong Thùng rác trước khi xóa hẳn */
    trashDays: number;
  };
}

export const DEFAULT_SETTINGS: CenterSettings = {
  crm: { defaultSlaMinutes: 15 },
  office: { from: '08:00', to: '17:30', satTo: '12:00', lateGraceMin: 5 },
  invoice: { codePattern: 'HD-{CN}-{YY}{MM}-{SO:4}', resetEvery: 'month' },
  payroll: { lockDay: 5 },
  atRisk: { absent: 3, streak: 2, attendanceRate: 80, submitRate: 50, scorePct: 60, mediumFrom: 3, highFrom: 5 },
  parent: { arrivalNotice: true, absentAlertMin: 15, underAge: 0, weeklyReport: true, weeklyDay: 0, weeklyTime: '20:00' },
  safety: { refundApprovalMin: 0, trashDays: 30 },
};

/** Nhãn từng mục — dùng cho nhật ký thay đổi */
export const SETTING_LABELS: Record<string, string> = {
  'crm.defaultSlaMinutes': 'SLA liên hệ lead mặc định (phút)',
  'office.from': 'Giờ hành chính — bắt đầu',
  'office.to': 'Giờ hành chính — kết thúc',
  'office.satTo': 'Thứ 7 làm đến',
  'office.lateGraceMin': 'Châm chước đi muộn (phút)',
  'invoice.codePattern': 'Mẫu mã hóa đơn',
  'invoice.resetEvery': 'Đánh số lại hóa đơn',
  'payroll.lockDay': 'Ngày khóa bảng lương',
  'atRisk.absent': 'Bỏ học — vắng không phép từ',
  'atRisk.streak': 'Bỏ học — vắng liên tiếp từ',
  'atRisk.attendanceRate': 'Bỏ học — chuyên cần dưới (%)',
  'atRisk.submitRate': 'Bỏ học — nộp bài dưới (%)',
  'atRisk.scorePct': 'Bỏ học — điểm dưới (%)',
  'atRisk.mediumFrom': 'Bỏ học — điểm rủi ro mức Trung bình',
  'atRisk.highFrom': 'Bỏ học — điểm rủi ro mức Cao',
  'parent.arrivalNotice': 'Báo phụ huynh khi con đến lớp',
  'parent.absentAlertMin': 'Báo chưa đến lớp sau (phút)',
  'parent.underAge': 'Chỉ báo cho học viên dưới (tuổi)',
  'parent.weeklyReport': 'Tự gửi báo cáo tuần',
  'parent.weeklyDay': 'Ngày gửi báo cáo tuần',
  'parent.weeklyTime': 'Giờ gửi báo cáo tuần',
  'safety.refundApprovalMin': 'Hoàn tiền cần duyệt từ (đ)',
  'safety.trashDays': 'Giữ trong Thùng rác (ngày)',
};

interface SettingsState {
  settings: CenterSettings;
  updatedAt?: string;
  updatedBy?: string;
  save: (next: CenterSettings, by: string) => number;
}

/** Danh sách thay đổi giữa 2 bản cài đặt (đường dẫn "nhóm.mục") */
export function diffSettings(a: CenterSettings, b: CenterSettings): NonNullable<AuditLog['changes']> {
  const out: NonNullable<AuditLog['changes']> = [];
  (Object.keys(b) as (keyof CenterSettings)[]).forEach((g) => {
    const ga = a[g] as Record<string, unknown>;
    const gb = b[g] as Record<string, unknown>;
    Object.keys(gb).forEach((k) => {
      if (String(ga[k] ?? '') !== String(gb[k] ?? '')) out.push({ field: SETTING_LABELS[`${g}.${k}`] ?? `${g}.${k}`, before: String(ga[k] ?? '—'), after: String(gb[k] ?? '—') });
    });
  });
  return out;
}

export const useSettingsStore = create<SettingsState>((set, get) => ({
  settings: DEFAULT_SETTINGS,
  save: (next, by) => {
    const changes = diffSettings(get().settings, next);
    if (!changes.length) return 0;
    set({ settings: next, updatedAt: new Date().toISOString(), updatedBy: by });
    // import động: tránh vòng import (settings được đọc từ nhiều module cấp thấp)
    void import('./activity').then(({ recordActivity }) => recordActivity({ category: 'setting', severity: 'warning', action: 'Đổi cài đặt chung', target: `${changes.length} mục`, changes }));
    return changes.length;
  },
}));

export const getSettings = () => useSettingsStore.getState().settings;
export function useSettings<T>(pick: (s: CenterSettings) => T): T {
  return useSettingsStore((st) => pick(st.settings));
}

// ============================== Mã hóa đơn ==============================

/**
 * Sinh mã hóa đơn theo mẫu. `existing` = mã đã có (để tìm số thứ tự kế tiếp trong cùng kỳ / chi nhánh).
 * VD mẫu "HD-{CN}-{YY}{MM}-{SO:4}" → HD-CG-2611-0007
 */
export function formatInvoiceCode(pattern: string, opts: { prefix: string; at?: dayjs.Dayjs; seq: number }): string {
  const at = opts.at ?? dayjs();
  return pattern
    .replace(/\{CN\}/g, opts.prefix)
    .replace(/\{YYYY\}/g, at.format('YYYY'))
    .replace(/\{YY\}/g, at.format('YY'))
    .replace(/\{MM\}/g, at.format('MM'))
    .replace(/\{SO(?::(\d))?\}/g, (_, n?: string) => String(opts.seq).padStart(Number(n ?? 1), '0'));
}

const esc = (x: string) => x.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** Mã kế tiếp: số thứ tự = lớn nhất trong các mã CÙNG KỲ (theo resetEvery) + 1 */
export function nextInvoiceCode(prefix: string, existing: string[], at = dayjs()): string {
  const { codePattern, resetEvery } = getSettings().invoice;
  // Regex nhận mã cùng kỳ: phần kỳ được đánh số lại thì cố định, phần còn lại thả tự do
  const re = new RegExp(
    `^${codePattern
      .split(/(\{CN\}|\{YYYY\}|\{YY\}|\{MM\}|\{SO(?::\d)?\})/)
      .map((part) => {
        if (part === '{CN}') return esc(prefix);
        if (part === '{YYYY}') return resetEvery === 'never' ? '\\d{4}' : at.format('YYYY');
        if (part === '{YY}') return resetEvery === 'never' ? '\\d{2}' : at.format('YY');
        if (part === '{MM}') return resetEvery === 'month' ? at.format('MM') : '\\d{2}';
        if (part.startsWith('{SO')) return '(\\d+)';
        return esc(part);
      })
      .join('')}$`,
  );
  const used = existing.map((c) => c.match(re)?.[1]).filter(Boolean).map(Number);
  return formatInvoiceCode(codePattern, { prefix, at, seq: (used.length ? Math.max(...used) : 0) + 1 });
}
