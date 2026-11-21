/**
 * Chế độ API: nạp dữ liệu Tuyển sinh sau khi đăng nhập (nhân viên). Trang không có quyền xem → KHÔNG gọi server, để rỗng (không lẫn dữ liệu giả);
 * lỗi khác không chặn phần còn lại.
 * Server chạy quét SLA / tự chuyển Sale, nhắc lịch hẹn, hết hạn link test (job mỗi phút) — trình duyệt không tự chạy nữa.
 */
import { loadLeads } from './crmApi';
import { useCrmStore } from './crmStore';
import { loadAssignmentLogs, loadAttempts, loadCampaigns, loadRules, loadTests } from './admission/admissionApi';
import { useAdmissionStore } from './admission/admissionStore';
import { loadAppointments } from './appointments/appointmentApi';
import { useAppointmentStore } from './appointments/appointments';
import { loadTemplates } from './messages/messageApi';

/** @param canView quyền xem 1 trang (vd "crm.leads") của người vừa đăng nhập */
export async function loadCrm(canView: (page: string) => boolean) {
  const quiet = (p: Promise<unknown>) => p.catch(() => undefined);
  const when = (page: string, load: () => Promise<unknown>, empty: () => void) => (canView(page) ? quiet(load()) : (empty(), Promise.resolve()));
  type Admission = ReturnType<typeof useAdmissionStore.getState>;
  const admission = (patch: Partial<Pick<Admission, 'rules' | 'logs' | 'tests' | 'attempts' | 'campaigns'>>) => () => useAdmissionStore.setState(patch);
  await Promise.all([
    when('crm.leads', loadLeads, () => useCrmStore.setState({ leads: [], consultations: [] })),
    when('crm.lead-rules', () => Promise.all([loadRules(), loadAssignmentLogs()]), admission({ rules: [], logs: [] })),
    when('crm.placement-tests', () => Promise.all([loadTests(), loadAttempts()]), admission({ tests: [], attempts: [] })),
    when('crm.campaigns', loadCampaigns, admission({ campaigns: [] })),
    when('crm.appointments', loadAppointments, () => useAppointmentStore.setState({ appointments: [] })),
    // Mẫu tin dùng cả ở trang Học viên (gửi tin cho HV / PH)
    quiet(loadTemplates()),
  ]);
}
