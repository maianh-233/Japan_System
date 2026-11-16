import { create } from 'zustand';
import { moveToTrash, registerRestorer } from '@/features/system/trash';

/**
 * MẪU TIN NHẮN Zalo / SMS — soạn sẵn cho các tình huống lặp lại, tự điền thông tin khi gửi.
 * Biến viết trong ngoặc nhọn: {ten}, {lop}, {so_tien}... (xem MESSAGE_VARS).
 * Khi có backend: gửi qua Zalo ZNS / nhà mạng SMS, lưu nhật ký gửi ở server.
 */

export type MessageScenario = 'welcome' | 'test_reminder' | 'consult_reminder' | 'tuition' | 'class_off' | 'arrival' | 'absent_alert' | 'weekly_report' | 'custom';
export type MessageChannel = 'zalo' | 'sms';
/** Gửi cho ai: lead (khách tiềm năng) hay học viên / phụ huynh */
export type MessageAudience = 'lead' | 'student';

export interface MessageTemplate {
  id: string;
  scenario: MessageScenario;
  name: string;
  /** Kênh gợi ý (vẫn đổi được khi gửi) */
  channel: MessageChannel;
  body: string;
  active: boolean;
  updatedAt: string;
  updatedBy: string;
}

export interface MessageLog {
  id: string;
  channel: MessageChannel;
  scenario: MessageScenario;
  templateId?: string;
  toName: string;
  toPhone: string;
  leadId?: string;
  studentId?: string;
  body: string;
  sentBy: string;
  sentAt: string;
  /** Hệ thống tự gửi (nhắc lịch hẹn) */
  auto?: boolean;
}

export const SCENARIOS: Record<MessageScenario, { label: string; audience: MessageAudience[]; hint: string }> = {
  welcome: { label: 'Chào lead mới', audience: ['lead'], hint: 'Gửi ngay khi có lead mới, trước cuộc gọi đầu tiên' },
  test_reminder: { label: 'Nhắc lịch test', audience: ['lead'], hint: 'Tự gửi trước giờ test 1 tiếng (lịch hẹn tại trung tâm)' },
  consult_reminder: { label: 'Nhắc lịch tư vấn', audience: ['lead'], hint: 'Tự gửi trước giờ hẹn tư vấn 1 tiếng' },
  tuition: { label: 'Nhắc học phí', audience: ['student'], hint: 'Học viên / phụ huynh còn nợ học phí' },
  class_off: { label: 'Báo nghỉ học', audience: ['student'], hint: 'Lớp nghỉ buổi học sắp tới' },
  arrival: { label: 'Báo con đã đến lớp', audience: ['student'], hint: 'Tự gửi phụ huynh khi con được điểm danh có mặt / đi muộn' },
  absent_alert: { label: 'Báo con chưa đến lớp', audience: ['student'], hint: 'Tự gửi khi quá giờ vào lớp mà chưa có mặt, không có đơn nghỉ' },
  weekly_report: { label: 'Báo cáo tuần', audience: ['student'], hint: 'Tự gửi phụ huynh cuối tuần — tóm tắt chuyên cần, bài tập, điểm, nhận xét' },
  custom: { label: 'Khác', audience: ['lead', 'student'], hint: 'Tin tự do' },
};

export const SCENARIO_OPTIONS = (Object.keys(SCENARIOS) as MessageScenario[]).map((k) => ({ value: k, label: SCENARIOS[k].label }));
export const CHANNEL_LABEL: Record<MessageChannel, string> = { zalo: 'Zalo', sms: 'SMS' };

/** Biến dùng được trong mẫu — `for` = có giá trị khi gửi cho ai */
export const MESSAGE_VARS: { key: string; label: string; example: string; for: MessageAudience[] }[] = [
  { key: 'ten', label: 'Tên người nhận', example: 'An', for: ['lead', 'student'] },
  { key: 'ho_ten', label: 'Họ tên người nhận', example: 'Nguyễn Văn An', for: ['lead', 'student'] },
  { key: 'hoc_vien', label: 'Tên học viên', example: 'Minh Khang', for: ['student'] },
  { key: 'khoa_hoc', label: 'Khóa quan tâm', example: 'JLPT N4', for: ['lead'] },
  { key: 'sale', label: 'Nhân viên phụ trách', example: 'Hồ Quỳnh Như', for: ['lead'] },
  { key: 'lop', label: 'Lớp', example: 'N4-K15', for: ['student'] },
  { key: 'so_tien', label: 'Số tiền còn nợ', example: '2.950.000đ', for: ['student'] },
  { key: 'han_dong', label: 'Hạn đóng', example: '20/11/2026', for: ['student'] },
  { key: 'ngay', label: 'Ngày (lịch hẹn / buổi học)', example: 'thứ Bảy 21/11', for: ['lead', 'student'] },
  { key: 'gio', label: 'Giờ', example: '18:30', for: ['lead', 'student'] },
  { key: 'phong', label: 'Phòng', example: 'P.201', for: ['lead'] },
  { key: 'chi_nhanh', label: 'Chi nhánh', example: 'Cầu Giấy', for: ['lead', 'student'] },
  { key: 'dia_chi', label: 'Địa chỉ chi nhánh', example: '120 Xuân Thủy, Cầu Giấy', for: ['lead', 'student'] },
  { key: 'hotline', label: 'Hotline chi nhánh', example: '024 3999 8888', for: ['lead', 'student'] },
  { key: 'gio_vao', label: 'Giờ vào lớp (điểm danh)', example: '18:04', for: ['student'] },
  { key: 'tinh_trang', label: 'Tình trạng (đúng giờ / đi muộn)', example: 'đúng giờ', for: ['student'] },
  { key: 'tuan', label: 'Tuần báo cáo', example: '09/11–15/11', for: ['student'] },
  { key: 'tom_tat', label: 'Tóm tắt tuần (tự tạo)', example: 'Đi học 3/3 buổi · Bài tập 2/2 · Điểm TB 8,5/10', for: ['student'] },
  { key: 'link', label: 'Link xem chi tiết', example: 'jpcenter.vn/bc/AB12', for: ['student'] },
];

/** Điền biến: {ten} → giá trị. Biến chưa có giá trị giữ nguyên để người gửi thấy và sửa tay. */
export function fillTemplate(body: string, vars: Record<string, string | undefined>): string {
  return body.replace(/\{([a-z_]+)\}/g, (m, k: string) => vars[k] ?? m);
}

/** Biến còn sót trong nội dung (chưa có dữ liệu) */
export const missingVars = (text: string) => [...new Set([...text.matchAll(/\{([a-z_]+)\}/g)].map((m) => m[1]))];

/** Bỏ dấu tiếng Việt — SMS có dấu tốn gấp đôi số tin */
export const stripAccents = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D');

/** Số tin SMS: không dấu 160 ký tự / tin (ghép: 153), có dấu (Unicode) 70 / tin (ghép: 67) */
export function smsSegments(text: string): { chars: number; segments: number; unicode: boolean } {
  const unicode = /[^\u0000-\u007f]/.test(text);
  const chars = text.length;
  const [single, multi] = unicode ? [70, 67] : [160, 153];
  return { chars, unicode, segments: chars === 0 ? 0 : chars <= single ? 1 : Math.ceil(chars / multi) };
}

/** Tên gọi: lấy chữ cuối của họ tên ("Nguyễn Văn An" → "An") */
export const firstName = (full: string) => full.trim().split(/\s+/).pop() ?? full;

const now = () => new Date().toISOString();
let seq = 0;

const T = (id: string, scenario: MessageScenario, name: string, channel: MessageChannel, body: string): MessageTemplate => ({
  id,
  scenario,
  name,
  channel,
  body,
  active: true,
  updatedAt: '2026-11-01T09:00:00.000Z',
  updatedBy: 'Phạm Thu Trang',
});

const DEFAULT_TEMPLATES: MessageTemplate[] = [
  T('MT-1', 'welcome', 'Chào lead mới (Zalo)', 'zalo', 'Chào {ten}! Mình là {sale} từ JP Center chi nhánh {chi_nhanh}. Cảm ơn bạn đã quan tâm khóa {khoa_hoc}. Mình sẽ gọi tư vấn cho bạn trong ít phút nữa, hoặc bạn nhắn lại thời gian thuận tiện nhé. Hotline: {hotline}.'),
  T('MT-2', 'welcome', 'Chào lead mới (SMS ngắn)', 'sms', 'JP Center: Chao {ten}, cam on ban da quan tam khoa {khoa_hoc}. Tu van vien {sale} se goi lai som. Hotline {hotline}.'),
  T('MT-3', 'test_reminder', 'Nhắc lịch test xếp lớp', 'zalo', 'Chào {ten}, JP Center nhắc bạn lịch TEST XẾP LỚP lúc {gio} {ngay} tại phòng {phong}, {dia_chi}. Bài test khoảng 45 phút, bạn mang theo bút và đến sớm 10 phút nhé. Cần đổi lịch, gọi {hotline}.'),
  T('MT-4', 'consult_reminder', 'Nhắc lịch tư vấn tại trung tâm', 'zalo', 'Chào {ten}, {sale} hẹn gặp bạn lúc {gio} {ngay} tại JP Center {chi_nhanh} ({dia_chi}) để tư vấn lộ trình học. Hẹn gặp bạn! Hotline: {hotline}.'),
  T('MT-5', 'tuition', 'Nhắc học phí', 'zalo', 'Chào {ten}, JP Center xin nhắc học phí lớp {lop} của {hoc_vien} còn {so_tien}, hạn đóng {han_dong}. Bạn có thể chuyển khoản / quét VietQR trên Cổng học viên hoặc đóng tại quầy. Cảm ơn bạn!'),
  T('MT-6', 'class_off', 'Báo nghỉ học', 'zalo', 'Chào {ten}, lớp {lop} của {hoc_vien} NGHỈ buổi {ngay} ({gio}). Buổi học bù sẽ được thông báo sau. Xin lỗi vì sự bất tiện. Hotline: {hotline}.'),
  T('MT-7', 'arrival', 'Con đã đến lớp', 'zalo', 'JP Center: {hoc_vien} đã đến lớp {lop} lúc {gio_vao} ({tinh_trang}). Buổi học {gio}.'),
  T('MT-8', 'absent_alert', 'Con chưa đến lớp', 'zalo', 'JP Center: Lớp {lop} bắt đầu lúc {gio} nhưng trung tâm chưa thấy {hoc_vien} đến lớp. Nếu con nghỉ, phụ huynh vui lòng báo lại giúp trung tâm. Hotline: {hotline}.'),
  T('MT-9', 'weekly_report', 'Báo cáo tuần', 'zalo', 'JP Center — Báo cáo tuần {tuan} của {hoc_vien} (lớp {lop}): {tom_tat}. Xem chi tiết: {link}'),
];

interface MessageState {
  templates: MessageTemplate[];
  logs: MessageLog[];
  saveTemplate: (t: Omit<MessageTemplate, 'id' | 'updatedAt'> & { id?: string }) => void;
  removeTemplate: (id: string) => void;
  send: (log: Omit<MessageLog, 'id' | 'sentAt'>) => MessageLog;
}

export const useMessageStore = create<MessageState>((set, get) => ({
  templates: DEFAULT_TEMPLATES,
  logs: [],
  saveTemplate: ({ id, ...t }) =>
    set((s) =>
      id
        ? { templates: s.templates.map((x) => (x.id === id ? { ...x, ...t, updatedAt: now() } : x)) }
        : { templates: [...s.templates, { ...t, id: `MT-${Date.now()}${++seq}`, updatedAt: now() }] },
    ),
  removeTemplate: (id) => {
    const t = get().templates.find((x) => x.id === id);
    if (t) moveToTrash({ kind: 'message_template', label: t.name, detail: SCENARIOS[t.scenario].label, payload: t });
    set((s) => ({ templates: s.templates.filter((x) => x.id !== id) }));
  },
  send: (l) => {
    const log: MessageLog = { ...l, id: `MSG-${Date.now()}${++seq}`, sentAt: now() };
    set((s) => ({ logs: [log, ...s.logs] }));
    return log;
  },
}));

registerRestorer<MessageTemplate>('message_template', (t) => useMessageStore.setState((s) => ({ templates: [...s.templates, t] })));
