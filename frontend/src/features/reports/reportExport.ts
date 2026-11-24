import { LEAD_STATUS_OPTIONS } from '@/features/crm/constants';
import { normalizeText } from '@/lib/text';
import { buildWorkbook, downloadBlob, type Sheet } from '@/lib/spreadsheet';
import type { ReportData } from './useReports';

/**
 * BC-07 — ghi báo cáo ra 1 file .xlsx, mỗi tab 1 sheet (Tuyển sinh · Doanh thu & công nợ · Đào tạo · So sánh chi nhánh).
 * Số ghi không ký hiệu ₫ / % (đơn vị ở tiêu đề cột); tên file `bao-cao_<chi-nhanh>_<YYYY-MM>_<N>-thang.xlsx`.
 */
const n = (v: number | undefined) => (v === undefined ? '' : String(v));

export function reportFileName(scope: string | undefined, monthKey: string, months: number) {
  return `bao-cao_${scope ? normalizeText(scope).replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') : 'toan-he-thong'}_${monthKey}_${months}-thang.xlsx`;
}

export function downloadReport(r: ReportData, months: number, scope: string | undefined) {
  const keys = r.monthKeys;
  const crm: Sheet = [
    ['Chỉ số', 'Giá trị'],
    ['Lead mới', n(r.lead.total)],
    ['Đã chốt', n(r.lead.won)],
    ['Tỷ lệ chuyển đổi (%)', n(r.lead.rate)],
    [],
    ['Tháng', 'Lead mới'],
    ...r.lead.byMonth.map((p) => [p.month, n(p.value)]),
    [],
    ['Trạng thái (lead tạo trong kỳ)', 'Số lead'],
    ...r.lead.funnel.map((f) => [LEAD_STATUS_OPTIONS.find((o) => o.value === f.key)?.label ?? f.key, n(f.value)]),
    [],
    ['Nguồn', 'Lead', 'Chốt', 'Tỷ lệ (%)'],
    ...r.lead.bySource.map((s) => [s.label, n(s.leads), n(s.won), n(s.rate)]),
    [],
    ['Sale', 'Lead', 'Đang mở', 'Chốt', 'Tỷ lệ (%)'],
    ...r.lead.bySale.map((s) => [s.label, n(s.leads), n(s.open), n(s.won), n(s.rate)]),
    [],
    ['Chiến dịch', 'Tên', 'Đã chi (đ)', 'Lead', 'Chốt', 'Chi phí / lead (đ)', 'Chi phí / HV chốt (đ)'],
    ...r.lead.byCampaign.map((c) => [c.label, c.name, n(c.spent), n(c.leads), n(c.won), n(c.cpl), n(c.cpa)]),
  ];
  const money: Sheet = [
    ['Tháng', 'Thực thu (đ)'],
    ...r.money.revenue.map((p) => [p.month, n(p.value)]),
    [],
    ['Thực thu tháng này (đ)', n(r.money.thisMonth)],
    ['Thực thu tháng trước (đ)', n(r.money.prevMonth)],
    [`Thực thu ${months} tháng (đ)`, n(r.money.collected)],
    [`Doanh số ký ${months} tháng (đ)`, n(r.money.booked)],
    ['Công nợ (đ)', n(r.money.debt)],
    ['Quá hạn (đ)', n(r.money.overdue)],
    [],
    ['Tuổi nợ', 'Số hóa đơn', 'Số tiền (đ)'],
    ...r.money.aging.map((a) => [a.label, n(a.count), n(a.amount)]),
  ];
  const study: Sheet = [
    ['Tháng', 'Ghi danh mới', 'Chuyên cần (%)'],
    ...keys.map((m, i) => [m, n(r.study.newEnroll[i]?.value), r.study.attendanceByMonth[i]?.empty ? '' : n(r.study.attendanceByMonth[i]?.value)]),
    [],
    ['HV đang học', n(r.study.studying)],
    ['Lớp đang học', n(r.study.ongoing)],
    [],
    ['Lớp', 'Khóa học', 'HV', 'Sĩ số tối đa', 'Lấp đầy (%)', 'Trạng thái'],
    ...r.study.fill.map((c) => [c.label, c.course, n(c.enrolled), n(c.max), n(c.rate), c.status === 'enrolling' ? 'Chờ khai giảng' : 'Đang học']),
    [],
    ['Phòng', 'Giờ có lớp tuần này', 'Công suất (%)'],
    ...r.study.roomUse.map((x) => [x.label, n(x.hours), n(x.rate)]),
  ];
  const branches: Sheet = [
    ['Chi nhánh', 'Lead', 'Chốt (%)', 'HV đang học', 'Lớp', 'Lấp đầy (%)', 'Chuyên cần (%)', `Thực thu ${months} tháng (đ)`, 'Thực thu tháng này (đ)', 'Công nợ (đ)', 'Quá hạn (đ)'],
    ...r.branches.map((b) => [b.label, n(b.leads), n(b.conv), n(b.students), n(b.classes), n(b.fill), n(b.attendance), n(b.collected), n(b.thisMonth), n(b.debt), n(b.overdue)]),
  ];
  const blob = buildWorkbook([
    { name: 'Tuyển sinh', rows: crm, widths: [30, 22, 14, 10, 10, 18, 20] },
    { name: 'Doanh thu & công nợ', rows: money, widths: [28, 16, 16] },
    { name: 'Đào tạo', rows: study, widths: [16, 24, 14, 14, 14, 16] },
    { name: 'So sánh chi nhánh', rows: branches, widths: [18, 8, 10, 12, 8, 12, 14, 20, 20, 16, 16] },
  ]);
  downloadBlob(blob, reportFileName(scope, keys[keys.length - 1] ?? '', months));
}
