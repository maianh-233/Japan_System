/**
 * Chứng nhận hoàn thành khóa (HV-15, giai đoạn 23) — backend Students: /api/v1/students/certificates, /enrollments/{id}/certificate,
 * tra cứu công khai /public/certificates/{code}, cổng HV / PH /portal/learning/certificates.
 * Checklist điều kiện do server tính (chuyên cần, bài thi cuối, học phí) — trang không tự tính lại ở chế độ API.
 */
import { api } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useStudentStore } from './studentStore';
import type { Certificate, EnrollmentStatus } from './types';

export interface CertCheck {
  key: string;
  ok: boolean;
  label: string;
}

/** 1 dòng trang Chứng nhận: HV × lớp đã hoàn thành */
export interface CertRow {
  enrollmentId: string;
  studentId: string;
  studentCode: string;
  studentName: string;
  email?: string;
  classId: string;
  classCode?: string;
  className?: string;
  endedAt?: string;
  checks: CertCheck[];
  eligible: boolean;
  certificate?: Certificate;
}

interface ServerCertificate {
  code: string;
  issuedAt: string;
  sentTo: string;
  sentBy?: string;
  sendCount?: number;
  exception?: string | null;
}

interface ServerRow {
  enrollmentId: string;
  studentId: string;
  studentCode: string;
  studentName: string;
  email?: string | null;
  classId: string;
  classCode?: string | null;
  className?: string | null;
  endedAt?: string | null;
  checks: CertCheck[];
  eligible: boolean;
  certificate?: ServerCertificate | null;
}

interface SendResult {
  enrollment: { id: string; certificate?: ServerCertificate | null };
  emailSent: boolean;
}

const toCert = (c?: ServerCertificate | null): Certificate | undefined =>
  c ? { code: c.code, issuedAt: c.issuedAt, sentTo: c.sentTo, sentBy: c.sentBy ?? '', sendCount: c.sendCount ?? 1, exception: c.exception ?? undefined } : undefined;

const toRow = (r: ServerRow): CertRow => ({
  enrollmentId: fromGuid(r.enrollmentId),
  studentId: fromGuid(r.studentId),
  studentCode: r.studentCode,
  studentName: r.studentName,
  email: r.email ?? undefined,
  classId: fromGuid(r.classId),
  classCode: r.classCode ?? undefined,
  className: r.className ?? undefined,
  endedAt: r.endedAt ?? undefined,
  checks: r.checks,
  eligible: r.eligible,
  certificate: toCert(r.certificate),
});

/** Ghi chứng nhận server trả vào ghi danh đang có trong store (tab HV của lớp, hồ sơ HV) */
function patch(results: SendResult[]) {
  const map = new Map(results.map((r) => [fromGuid(r.enrollment.id), toCert(r.enrollment.certificate)]));
  useStudentStore.setState((s) => ({ enrollments: s.enrollments.map((e) => (map.has(e.id) ? { ...e, certificate: map.get(e.id) } : e)) }));
}

/** HV-15 bước 1 — HV đã hoàn thành lớp (trong phạm vi chi nhánh) + checklist */
export const loadCertificateRows = async () => (await api<ServerRow[]>('/students/certificates')).map(toRow);

/** HV-15 bước 2 — cấp & gửi 1 HV; chưa đủ điều kiện cần lý do ngoại lệ. Trả `emailSent` (lỗi gửi vẫn giữ chứng nhận) */
export async function issueCertificate(enrollmentId: string, email: string, exception?: string) {
  const r = await api<SendResult>(`/students/enrollments/${toGuid(enrollmentId)}/certificate`, { body: { email, exception: exception ?? null } });
  patch([r]);
  return { emailSent: r.emailSent, code: r.enrollment.certificate?.code };
}

/** HV-15 bước 3 — gửi lại (email có thể khác) */
export async function resendCertificate(enrollmentId: string, email: string) {
  const r = await api<SendResult>(`/students/enrollments/${toGuid(enrollmentId)}/certificate/resend`, { body: { email } });
  patch([r]);
  return { emailSent: r.emailSent, code: r.enrollment.certificate?.code };
}

/** "Cấp & gửi" nhiều HV đủ điều kiện — 1 request; HV không cấp được nằm trong `skipped` */
export async function issueCertificates(items: { enrollmentId: string; email: string }[]) {
  const r = await api<{ issued: SendResult[]; skipped: { enrollmentId: string; code: string; message: string }[] }>('/students/certificates/issue', {
    body: { items: items.map((i) => ({ enrollmentId: toGuid(i.enrollmentId), email: i.email })) },
  });
  patch(r.issued);
  return {
    issued: r.issued.length,
    mailFailed: r.issued.filter((x) => !x.emailSent).length,
    skipped: r.skipped.map((x) => ({ enrollmentId: fromGuid(x.enrollmentId), message: x.message })),
  };
}

export interface CertVerification {
  code: string;
  studentName: string;
  classCode?: string;
  className?: string;
  level?: string;
  issuedAt: string;
}

/** HV-15 bước 4 — tra cứu mã (công khai, không phân biệt hoa / thường); không thấy → null */
export async function verifyCertificate(code: string): Promise<CertVerification | null> {
  try {
    return await api<CertVerification>(`/students/public/certificates/${encodeURIComponent(code.trim())}`);
  } catch (e) {
    if ((e as { status?: number }).status === 404) return null;
    throw e;
  }
}

/** Cổng HV / PH (HV-15.E5) — lớp đang học / đã hoàn thành của HV + checklist + chứng nhận đã cấp */
export interface MyCertificate {
  enrollmentId: string;
  classId: string;
  status: EnrollmentStatus;
  endedAt?: string;
  checks: CertCheck[];
  eligible: boolean;
  certificate?: { code: string; issuedAt: string; sentTo: string };
}

export async function loadMyCertificates(studentId?: string): Promise<MyCertificate[]> {
  const list = await api<(Omit<MyCertificate, 'enrollmentId' | 'classId' | 'endedAt' | 'certificate'> & {
    enrollmentId: string;
    classId: string;
    endedAt?: string | null;
    certificate?: { code: string; issuedAt: string; sentTo: string } | null;
  })[]>(`/students/portal/learning/certificates${studentId ? `?studentId=${toGuid(studentId)}` : ''}`);
  return list.map((c) => ({
    ...c,
    enrollmentId: fromGuid(c.enrollmentId),
    classId: fromGuid(c.classId),
    endedAt: c.endedAt ?? undefined,
    certificate: c.certificate ?? undefined,
  }));
}
