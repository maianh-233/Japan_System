/**
 * Chế độ API: nạp dữ liệu Tuyển sinh sau khi đăng nhập (nhân viên). Mỗi phần không có quyền → để rỗng, lỗi khác không chặn phần còn lại.
 * Server chạy quét SLA / tự chuyển Sale, nhắc lịch hẹn, hết hạn link test (job mỗi phút) — trình duyệt không tự chạy nữa.
 */
import { loadLeads } from './crmApi';
import { loadAssignmentLogs, loadAttempts, loadCampaigns, loadRules, loadTests } from './admission/admissionApi';
import { loadAppointments } from './appointments/appointmentApi';
import { loadTemplates } from './messages/messageApi';

export async function loadCrm() {
  const quiet = (p: Promise<unknown>) => p.catch(() => undefined);
  await Promise.all([
    quiet(loadLeads()),
    quiet(loadRules()),
    quiet(loadAssignmentLogs()),
    quiet(loadTests()),
    quiet(loadAttempts()),
    quiet(loadCampaigns()),
    quiet(loadAppointments()),
    quiet(loadTemplates()),
  ]);
}
