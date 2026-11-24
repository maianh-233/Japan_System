/**
 * Xuất dữ liệu giả (mock) của frontend thành JSON để backend nạp vào PostgreSQL làm dữ liệu mẫu.
 * Dữ liệu mẫu backend vì thế giống hệt những gì giao diện đang hiển thị.
 *
 * Chạy (từ thư mục frontend):  npm run export-seed
 * Kết quả: backend/src/Modules/{System,Hr,Academic,Students,Crm,Finance}/.../Infrastructure/Persistence/Seed/*.seed.json
 * Thời gian tương đối (dayjs "x ngày trước") được chốt tại lúc chạy script.
 */
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { MOCK_EMPLOYEES } from '@/mocks/data/employees';
import { MOCK_COURSES } from '@/mocks/data/courses';
import { MOCK_COURSE_CURRICULA, MOCK_CURRICULA } from '@/mocks/data/curricula';
import { MOCK_CLASSES } from '@/mocks/data/classes';
import { MOCK_SESSIONS } from '@/mocks/data/attendance';
import { MOCK_ROOM_ISSUES, MOCK_ROOMS } from '@/mocks/data/rooms';
import { MOCK_QUESTIONS } from '@/mocks/data/questions';
import { MOCK_CONSULTATIONS, MOCK_LEADS, MOCK_LINKS } from '@/mocks/data/crm';
import { MOCK_ACCOUNTS as PORTAL_ACCOUNTS, MOCK_ENROLLMENTS, MOCK_STUDENTS } from '@/mocks/data/students';
import {
  MOCK_AUDIT_LOGS,
  MOCK_BRANCH_PROFILES,
  MOCK_INTEGRATION_LOGS,
  MOCK_INTEGRATIONS,
  MOCK_ROLES,
  MOCK_USER_ACCESS,
} from '@/mocks/data/system';
import { MOCK_ACCOUNTS as DEMO_ACCOUNTS } from '@/mocks/data/users';
import { PERM_CATALOG } from '@/features/system/constants';

const backend = resolve(process.cwd(), '..', 'backend', 'src', 'Modules');

function write(path: string, data: unknown) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, JSON.stringify(data, null, 2) + '\n', 'utf8');
  console.log('✓', path);
}

// Email đăng nhập demo (superadmin@, giaovu@...) thay cho email hồ sơ để đăng nhập giống bản mock
const demoEmailByEmployee = new Map(DEMO_ACCOUNTS.filter((a) => a.employeeId).map((a) => [a.employeeId!, a.email]));
// Tài khoản demo học viên / phụ huynh → gắn vào tài khoản cổng tương ứng
const DEMO_PORTAL: Record<string, string> = { 'ST-1': 'hocvien@jpcenter.vn', 'L-0001': 'phuhuynh@jpcenter.vn' };

const studentName = new Map(MOCK_STUDENTS.map((s) => [s.id, s.fullName]));
const leadName = new Map(MOCK_LEADS.map((l) => [l.id, l.fullName]));

// Danh mục quyền (module → trang → hành động) — backend dùng để kiểm tra mã quyền hợp lệ, KHÔNG phải dữ liệu mẫu
write(resolve(backend, 'System', 'JPCenter.Modules.System', 'Infrastructure', 'Services', 'permission-catalog.json'), {
  generatedAt: new Date().toISOString(),
  globalOnlyModules: ['system'],
  modules: PERM_CATALOG,
});

write(resolve(backend, 'Hr', 'JPCenter.Modules.Hr', 'Infrastructure', 'Persistence', 'Seed', 'hr.seed.json'), {
  generatedAt: new Date().toISOString(),
  employees: MOCK_EMPLOYEES,
});

// Khóa học + lộ trình (user story 09). Người soạn: tên → Id nhân viên
const employeeIdByName = new Map(MOCK_EMPLOYEES.map((e) => [e.fullName, e.id]));
write(resolve(backend, 'Academic', 'JPCenter.Modules.Academic', 'Infrastructure', 'Persistence', 'Seed', 'academic.seed.json'), {
  generatedAt: new Date().toISOString(),
  courses: MOCK_COURSES,
  curricula: MOCK_CURRICULA.map((c) => ({ ...c, authorId: employeeIdByName.get(c.author) ?? null })),
  courseCurricula: MOCK_COURSE_CURRICULA,
});

// Ngân hàng câu hỏi (user story 11). Người soạn / người duyệt lưu theo TÊN → Id nhân viên; bài học giữ Id mock ("CUR-N5-V2-L3" = bài thứ 3)
const nameId = (name?: string) => (name ? (employeeIdByName.get(name.replace(/\s*\(.*\)\s*$/, '').trim()) ?? null) : null);
write(resolve(backend, 'Academic', 'JPCenter.Modules.Academic', 'Infrastructure', 'Persistence', 'Seed', 'questions.seed.json'), {
  generatedAt: new Date().toISOString(),
  questions: MOCK_QUESTIONS.map((q) => ({
    id: q.id, code: q.code, level: q.level, skill: q.skill, format: q.format, difficulty: q.difficulty, stem: q.stem, passage: q.passage,
    audio: q.audio, transcript: q.transcript, options: q.options, answer: q.answer, explanation: q.explanation, curriculumId: q.curriculumId,
    lessonIds: q.lessonIds, tags: q.tags, status: q.status, authorId: nameId(q.author), createdAt: q.createdAt,
    review: q.review ? { byId: nameId(q.review.by), at: q.review.at, note: q.review.note } : null,
    stats: q.stats,
  })),
});

// Phòng, lớp, buổi học (user story 07). Người thao tác lưu theo TÊN ("Phạm Thị Hồng (Giáo vụ)") → Id nhân viên nếu có, không có → null
const personId = (name?: string) => (name ? (employeeIdByName.get(name.replace(/\s*\(.*\)\s*$/, '').trim()) ?? null) : null);
write(resolve(backend, 'Academic', 'JPCenter.Modules.Academic', 'Infrastructure', 'Persistence', 'Seed', 'classes.seed.json'), {
  generatedAt: new Date().toISOString(),
  rooms: MOCK_ROOMS,
  roomIssues: MOCK_ROOM_ISSUES.map((i) => ({ ...i, reportedById: personId(i.reportedBy) })),
  classes: MOCK_CLASSES,
  sessions: MOCK_SESSIONS.map((x) => ({
    ...x,
    attendanceById: personId(x.attendanceBy?.name),
    confirmedById: personId(x.confirmation?.by),
    cancelledById: personId(x.cancelledBy?.name),
    supportLog: x.supportLog?.map((e) => ({ ...e, byId: personId(e.by) })),
    incident: x.incident && { ...x.incident, reportedById: personId(x.incident.reportedBy), outcomeById: personId(x.incident.outcome?.by) },
    teacherLog: x.teacherLog && { ...x.teacherLog, authorId: personId(x.teacherLog.author) },
  })),
});

// Học viên + ghi danh + chứng nhận (user story 06). Người gửi chứng nhận: tên → Id nhân viên
write(resolve(backend, 'Students', 'JPCenter.Modules.Students', 'Infrastructure', 'Persistence', 'Seed', 'students.seed.json'), {
  generatedAt: new Date().toISOString(),
  students: MOCK_STUDENTS,
  // Chi nhánh của LỚP (Students không đọc bảng Academic)
  enrollments: MOCK_ENROLLMENTS.map((e) => ({
    ...e,
    branchId: MOCK_CLASSES.find((c) => c.id === e.classId)?.branchId,
    certificate: e.certificate && { ...e.certificate, sentById: personId(e.certificate.sentBy) },
  })),
});

// Đơn hủy buổi, lời mời dạy thay, đơn xin nghỉ, lên lớp, nhận xét cuối khóa (user story 07 — giai đoạn 15).
// Import ĐỘNG sau khi đã ghi classes.seed.json: mock đơn xin nghỉ sửa điểm danh của MOCK_SESSIONS lúc nạp module —
// backend tự áp hiệu lực đơn (vắng có phép, khách học bù) bằng method domain, nên dữ liệu buổi học xuất ở trên phải là bản gốc.
const { MOCK_CANCEL_REQUESTS, MOCK_SUB_INVITES, MOCK_LEAVE_REQUESTS } = await import('@/mocks/data/portal');
const { MOCK_PROMOTIONS } = await import('@/mocks/data/promotions');
const { MOCK_EVALUATIONS, MOCK_CARE_LOGS } = await import('@/mocks/data/classExtras');
const { useAtRiskStore } = await import('@/features/student/atRisk/atRisk');
const classBranch = (classId: string) => MOCK_CLASSES.find((c) => c.id === classId)?.branchId ?? null;
/** Tài khoản cổng gửi đơn: HV → tài khoản của HV; PH → tài khoản PH có tên trùng người gửi */
const portalAccountOf = (studentId: string, by: 'student' | 'parent', byName: string) =>
  (by === 'student'
    ? PORTAL_ACCOUNTS.find((a) => a.ownerType === 'student' && a.ownerId === studentId)
    : PORTAL_ACCOUNTS.find((a) => a.ownerType !== 'student' && leadName.get(a.ownerId) === byName)
  )?.id ?? null;
write(resolve(backend, 'Academic', 'JPCenter.Modules.Academic', 'Infrastructure', 'Persistence', 'Seed', 'requests.seed.json'), {
  generatedAt: new Date().toISOString(),
  cancelRequests: MOCK_CANCEL_REQUESTS.map((r) => ({ ...r, branchId: classBranch(r.classId), decidedById: personId(r.decidedBy) })),
  subInvites: MOCK_SUB_INVITES.map((i) => ({ ...i, branchId: classBranch(i.classId), invitedById: personId(i.invitedBy) })),
  leaveRequests: MOCK_LEAVE_REQUESTS.map((r) => ({
    ...r,
    branchId: classBranch(r.classId),
    byAccountId: portalAccountOf(r.studentId, r.by, r.byName),
    decidedById: personId(r.decidedBy),
  })),
  promotions: MOCK_PROMOTIONS.map((p) => ({
    ...p,
    branchId: classBranch(p.fromClassId),
    notes: p.notes.map((n) => ({ ...n, byId: personId(n.by) })),
  })),
  evaluations: MOCK_EVALUATIONS,
});

// Tuyển sinh (user story 05 — giai đoạn 18): lead, lịch sử tư vấn, liên kết HV, quy tắc chia lead, test xếp lớp, lịch hẹn, chiến dịch, mẫu tin
const { MOCK_ASSIGN_LOGS, MOCK_ATTEMPTS, MOCK_CAMPAIGNS, MOCK_LEAD_RULES, MOCK_PLACEMENT_TESTS } = await import('@/mocks/data/admissions');
const { useAppointmentStore } = await import('@/features/crm/appointments/appointments');
const { useMessageStore } = await import('@/features/crm/messages/messages');
write(resolve(backend, 'Crm', 'JPCenter.Modules.Crm', 'Infrastructure', 'Persistence', 'Seed', 'crm.seed.json'), crmSeed());

// Tài chính đợt 1 (user story 08 — giai đoạn 19): bảng giá, ưu đãi, chính sách đợt, hóa đơn (+ thu / hoàn), giao dịch ngân hàng, nhắc học phí
const fin = await import('@/mocks/data/finance');
const { MOCK_INVOICES } = await import('@/mocks/data/students');
write(resolve(backend, 'Finance', 'JPCenter.Modules.Finance', 'Infrastructure', 'Persistence', 'Seed', 'finance.seed.json'), financeSeed());

// Lương + hoa hồng (giai đoạn 22): điều chỉnh lương, bảng lương đã chốt, bậc hoa hồng, chỉ tiêu Sale, Sale ghi nhận của từng hóa đơn (TC-04.E5)
const { saleOfInvoice, leadSaleMap } = await import('@/features/finance/financeUtils');
write(resolve(backend, 'Finance', 'JPCenter.Modules.Finance', 'Infrastructure', 'Persistence', 'Seed', 'payroll.seed.json'), payrollSeed());

// Nội dung học (user story 12 — giai đoạn 20): bài tập, bài nộp, kết quả thi, thông báo lớp, đánh giá GV, flashcard (Academic) · tài liệu GV (Library)
const { MOCK_ASSIGNMENTS } = await import('@/mocks/data/classes');
const { MOCK_SUBMISSIONS, MOCK_EXAM_RESULTS } = await import('@/mocks/data/submissions');
const { MOCK_TEACHER_REVIEWS, MOCK_ANNOUNCEMENTS } = await import('@/mocks/data/classExtras');
const { MOCK_DECKS, MOCK_CARD_PROGRESS, MOCK_TEACHER_DOCS } = await import('@/mocks/data/portal');
const learning = learningSeed();
write(resolve(backend, 'Academic', 'JPCenter.Modules.Academic', 'Infrastructure', 'Persistence', 'Seed', 'learning.seed.json'), learning.academic);
write(resolve(backend, 'Library', 'JPCenter.Modules.Library', 'Infrastructure', 'Persistence', 'Seed', 'library.seed.json'), learning.library);

// Nhân sự mở rộng + Công việc (user story 13, 14 — giai đoạn 21): chấm công, đơn từ, tuyển dụng, onboarding (Hr) · task, ca, bàn giao (Work)
const hrOps = await import('@/mocks/data/hr');
const workMock = await import('@/mocks/data/work');
const hrWork = hrWorkSeed();
write(resolve(backend, 'Hr', 'JPCenter.Modules.Hr', 'Infrastructure', 'Persistence', 'Seed', 'hr-ops.seed.json'), hrWork.hr);
write(resolve(backend, 'Work', 'JPCenter.Modules.Work', 'Infrastructure', 'Persistence', 'Seed', 'work.seed.json'), hrWork.work);

function hrWorkSeed() {
  const branchOf = (employeeId: string) => MOCK_EMPLOYEES.find((e) => e.id === employeeId)?.branchId ?? null;
  /** Đơn đổi ca mẫu không ghi ca → ca sắp tới đầu tiên của người gửi (từ ngày đơn) ↔ ca khác của đồng nghiệp cùng chi nhánh cùng ngày */
  const swapOf = (employeeId: string, from: string) => {
    const upcoming = workMock.MOCK_SHIFTS.filter((x) => x.date >= from).sort((a, b) => a.date.localeCompare(b.date));
    for (const mine of upcoming.filter((x) => x.employeeId === employeeId)) {
      const theirs = upcoming.find((x) => x.date === mine.date && x.employeeId !== employeeId && x.shift !== mine.shift && branchOf(x.employeeId) === branchOf(employeeId));
      if (theirs) return { myShiftId: mine.id, withEmployeeId: theirs.employeeId, theirShiftId: theirs.id };
    }
    return undefined;
  };
  return {
    hr: {
      generatedAt: new Date().toISOString(),
      timeRecords: hrOps.MOCK_TIME_RECORDS.map((r) => ({
        id: r.id, employeeId: r.employeeId, branchId: branchOf(r.employeeId), date: r.date, checkIn: r.checkIn ?? null, checkOut: r.checkOut ?? null,
        editedById: personId(r.edited?.by), editedByName: r.edited?.by ?? null, editReason: r.edited?.reason ?? null,
      })),
      requests: hrOps.MOCK_REQUESTS.map((r) => ({
        ...r,
        branchId: branchOf(r.employeeId),
        swap: r.swap ?? (r.type === 'shift_swap' ? swapOf(r.employeeId, r.from) : undefined),
        steps: r.steps.map((st) => ({ ...st, approverId: personId(st.approver) })),
      })),
      jobs: hrOps.MOCK_JOBS,
      candidates: hrOps.MOCK_CANDIDATES.map((c) => ({
        ...c,
        interview: c.interview ? { ...c.interview, interviewerId: personId(c.interview.interviewer) } : null,
        notes: c.notes.map((x) => ({ ...x, byId: personId(x.by) })),
      })),
      onboardings: hrOps.MOCK_ONBOARDINGS.map((o) => ({ ...o, mentorId: personId(o.mentor), items: o.items.map((i) => ({ ...i, doneById: personId(i.doneBy) })) })),
    },
    work: {
      generatedAt: new Date().toISOString(),
      shifts: workMock.MOCK_SHIFTS.map((x) => ({ ...x, branchId: branchOf(x.employeeId) })),
      registrations: workMock.MOCK_SHIFT_AVAILABILITY,
      tasks: workMock.MOCK_TASKS.map((t) => ({ ...t, branchId: branchOf(t.assigneeId), comments: t.comments.map((c) => ({ ...c, byId: personId(c.by) })) })),
      handovers: workMock.MOCK_HANDOVERS,
    },
  };
}

function learningSeed() {
  const teacherOf = (classId: string) => MOCK_CLASSES.find((c) => c.id === classId)?.teacherIds[0] ?? null;
  /** "Giáo vụ" / "Kế toán" (tên vai trò trong mock) → nhân viên phòng đó ở chi nhánh của lớp */
  const staffOf = (role: string, classId: string) => {
    const dept = role === 'Kế toán' ? 'finance' : 'academic';
    const branch = classBranch(classId);
    return (
      personId(role) ??
      MOCK_EMPLOYEES.find((e) => e.department === dept && e.branchId === branch && e.kind === 'staff')?.id ??
      MOCK_EMPLOYEES.find((e) => e.department === dept && e.kind === 'staff')?.id ??
      null
    );
  };
  /** "Bài 10" + N3 → bài thứ 10 của lộ trình N3 đầu tiên (Id bài `…-L10`) */
  const lessonOf = (level: string, label: string) => {
    const no = /(\d+)/.exec(label)?.[1];
    if (!no) return null;
    for (const c of MOCK_CURRICULA.filter((x) => x.level === level)) {
      const hit = c.lessons.find((l) => l.id.endsWith(`-L${no}`));
      if (hit) return hit.id;
    }
    return null;
  };
  const cardDeck = new Map(MOCK_DECKS.flatMap((d) => d.cards.map((c) => [c.id, d.id] as const)));
  return {
    academic: {
      generatedAt: new Date().toISOString(),
      assignments: MOCK_ASSIGNMENTS.map((a) => ({ ...a, createdById: teacherOf(a.classId) })),
      submissions: MOCK_SUBMISSIONS.map((x) => {
        const a = MOCK_ASSIGNMENTS.find((y) => y.id === x.assignmentId)!;
        const auto = a.type === 'quiz';
        return { ...x, gradedById: !auto && x.gradedAt ? teacherOf(a.classId) : null };
      }),
      examResults: MOCK_EXAM_RESULTS,
      announcements: MOCK_ANNOUNCEMENTS.map((x) => ({ ...x, sentById: staffOf(x.sentBy, x.classId) })),
      teacherReviews: MOCK_TEACHER_REVIEWS,
      decks: MOCK_DECKS.map((d) => ({ ...d, lessonId: lessonOf(d.level, d.lesson) })),
      // Tiến độ ôn của tài khoản demo học viên (ST-1)
      cardProgress: Object.entries(MOCK_CARD_PROGRESS).map(([cardId, p]) => ({ id: `CP-ST-1-${cardId}`, studentId: 'ST-1', deckId: cardDeck.get(cardId), cardId, ...p })),
    },
    library: {
      generatedAt: new Date().toISOString(),
      teacherDocs: MOCK_TEACHER_DOCS,
    },
  };
}

function financeSeed() {
  const enrollment = new Map(MOCK_ENROLLMENTS.map((e) => [e.id, e]));
  const prefix = new Map(MOCK_BRANCH_PROFILES.map((b) => [b.id, (b as { invoicePrefix?: string }).invoicePrefix ?? b.code]));
  // Mã mock "HD2400" → mã theo mẫu Cài đặt HD-{CN}-{YY}{MM}-{SO:4}, đánh số theo chi nhánh + tháng lập (theo thứ tự lập)
  const seq = new Map<string, number>();
  const codeOf = new Map<string, string>();
  [...MOCK_INVOICES]
    .sort((a, b) => a.issuedAt.localeCompare(b.issuedAt))
    .forEach((inv) => {
      const e = enrollment.get(inv.enrollmentId)!;
      const branch = classBranch(e.classId)!;
      const ym = inv.issuedAt.slice(2, 4) + inv.issuedAt.slice(5, 7);
      const key = `${branch}:${ym}`;
      const n = (seq.get(key) ?? 0) + 1;
      seq.set(key, n);
      codeOf.set(inv.id, `HD-${prefix.get(branch)}-${ym}-${String(n).padStart(4, '0')}`);
    });
  const recode = (text: string) => MOCK_INVOICES.reduce((t, inv) => t.replaceAll(inv.code, codeOf.get(inv.id)!), text);
  return {
    generatedAt: new Date().toISOString(),
    branchPrices: fin.MOCK_BRANCH_PRICES,
    discounts: fin.MOCK_PROMOTIONS,
    installmentPlans: fin.MOCK_INSTALLMENTS,
    invoices: MOCK_INVOICES.map((inv) => {
      const e = enrollment.get(inv.enrollmentId)!;
      const vietqr = inv.payments.filter((p) => p.method === 'vietqr');
      return {
        id: inv.id,
        code: codeOf.get(inv.id),
        enrollmentId: inv.enrollmentId,
        studentId: e.studentId,
        studentName: studentName.get(e.studentId) ?? '',
        classId: e.classId,
        branchId: classBranch(e.classId),
        amount: inv.amount,
        discount: inv.discount,
        dueDate: inv.dueDate,
        issuedAt: inv.issuedAt,
        // Thu bằng VietQR → khoản thu gắn giao dịch ngân hàng đã tự khớp (TX-A{hóa đơn}-{thứ tự})
        payments: inv.payments.map((p) => ({
          ...p,
          collectorId: personId(p.collector),
          bankTransactionId: p.method === 'vietqr' ? `TX-A${inv.id}-${vietqr.indexOf(p)}` : null,
        })),
        refunds: inv.refunds.map((r) => ({ ...r, byId: personId(r.by), approvedById: personId(r.approvedBy) })),
        cancelled: Boolean(inv.cancelled),
        cancelReason: inv.cancelled ? 'Học viên nghỉ học — hủy phần học phí còn lại' : null,
      };
    }),
    bankTransactions: fin.MOCK_BANK_TXNS.map((t) => ({
      ...t,
      content: recode(t.content),
      invoiceIds: t.invoiceId ? [t.invoiceId] : [],
      matchedById: t.matchedBy === 'Hệ thống' ? null : personId(t.matchedBy),
    })),
    reminders: fin.MOCK_REMINDERS.map((r) => ({ ...r, byId: r.by.startsWith('Hệ thống') ? null : personId(r.by) })),
  };
}

function payrollSeed() {
  const enrollment = new Map(MOCK_ENROLLMENTS.map((e) => [e.id, e]));
  const leadSale = leadSaleMap(MOCK_LEADS, MOCK_LINKS);
  return {
    generatedAt: new Date().toISOString(),
    payAdjustments: fin.MOCK_PAY_ADJUSTS.map((a) => ({ ...a, byId: personId(a.by) })),
    // Mock chốt tháng trước với tổng 0đ, chưa có chi tiết từng người — backend nạp nguyên (snapshot rỗng)
    payrollLocks: fin.MOCK_PAYROLL_LOCKS.map((l) => ({ ...l, lockedById: personId(l.lockedBy) })),
    commissionTiers: fin.MOCK_COMMISSION_TIERS,
    saleTargets: fin.MOCK_SALE_TARGETS,
    invoiceSales: MOCK_INVOICES.flatMap((inv) => {
      const e = enrollment.get(inv.enrollmentId);
      const branchId = e ? classBranch(e.classId) : null;
      const saleId = e && branchId ? saleOfInvoice(inv, e.studentId, branchId, MOCK_EMPLOYEES, {}, leadSale) : undefined;
      return saleId ? [{ invoiceId: inv.id, saleId }] : [];
    }),
  };
}

// Cảnh báo bỏ học (HV-14): trường hợp đã giao người chăm sóc + nhật ký liên hệ. Mức rủi ro / dấu hiệu do job backend tính lại
write(resolve(backend, 'Students', 'JPCenter.Modules.Students', 'Infrastructure', 'Persistence', 'Seed', 'care.seed.json'), {
  generatedAt: new Date().toISOString(),
  cases: Object.entries(useAtRiskStore.getState().cases).map(([key, c]) => {
    const [classId, studentId] = key.split(':');
    return { classId, studentId, branchId: classBranch(classId), assigneeId: personId(c.assignee), status: c.status, nextAt: c.nextAt, updatedAt: c.updatedAt };
  }),
  logs: MOCK_CARE_LOGS.map((l) => ({ ...l, branchId: classBranch(l.classId), byId: personId(l.by) })),
});

write(resolve(backend, 'System', 'JPCenter.Modules.System', 'Infrastructure', 'Persistence', 'Seed', 'system.seed.json'), {
  generatedAt: new Date().toISOString(),
  branches: MOCK_BRANCH_PROFILES,
  roles: MOCK_ROLES,
  staffAccounts: MOCK_USER_ACCESS.map((a) => ({
    ...a,
    email: demoEmailByEmployee.get(a.userId) ?? a.email,
    fullName: MOCK_EMPLOYEES.find((e) => e.id === a.userId)?.fullName ?? a.email,
  })),
  portalAccounts: PORTAL_ACCOUNTS.map((a) => ({
    ...a,
    email: DEMO_PORTAL[a.ownerId] ?? a.email,
    displayName: (a.ownerType === 'student' ? studentName.get(a.ownerId) : leadName.get(a.ownerId)) ?? a.username,
  })),
  integrations: MOCK_INTEGRATIONS,
  integrationLogs: MOCK_INTEGRATION_LOGS,
  auditLogs: MOCK_AUDIT_LOGS,
});

function crmSeed() {
  // Mã khóa quan tâm của mock ("n5", "kaiwa", "kids"…) → khóa học đầu tiên khớp (nhóm, hoặc JLPT theo trình độ đích)
  const courseOf = (code: string) =>
    MOCK_COURSES.find((c) => c.category === code || (c.category === 'jlpt' && c.levelTo.toLowerCase() === code))?.id ?? null;
  return {
    generatedAt: new Date().toISOString(),
    leads: MOCK_LEADS.map((l) => ({
      id: l.id, code: l.code, fullName: l.fullName, phone: l.phone, email: l.email, source: l.source, status: l.status, branchId: l.branchId,
      assigneeId: l.assigneeId, interest: l.interest, note: l.note, lostReason: l.lostReason, hidden: l.hidden, hiddenAt: l.hiddenAt,
      createdAt: l.createdAt, nextFollowUpAt: l.nextFollowUpAt, notifyChannels: l.notifyChannels ?? [], campaignId: l.campaignId,
      slaEscalatedAt: l.slaEscalatedAt, slaReassignedAt: l.slaReassignedAt,
    })),
    consultations: MOCK_CONSULTATIONS.map((c) => ({ ...c, staffId: personId(c.staffName) })),
    links: MOCK_LINKS.map((k) => ({ ...k, studentName: studentName.get(k.studentId) ?? k.studentId })),
    rules: MOCK_LEAD_RULES.map((r) => ({ ...r, escalateToIds: r.escalateTo.map(personId).filter(Boolean) })),
    assignmentLogs: MOCK_ASSIGN_LOGS,
    placementTests: MOCK_PLACEMENT_TESTS.map((t) => ({ ...t, authorId: personId(t.author) })),
    attempts: MOCK_ATTEMPTS.map((a) => ({ ...a, sentById: personId(a.sentBy) })),
    appointments: useAppointmentStore.getState().appointments,
    campaigns: MOCK_CAMPAIGNS.map((c) => ({
      ...c, ownerId: personId(c.owner), courseIds: c.courses.map(courseOf).filter(Boolean),
    })),
    templates: useMessageStore.getState().templates,
  };
}

// Bảng đổi Id mock ↔ GUID backend (giống SeedData.IdOf: MD5 "jpcenter-seed:" + id, đánh dấu UUID v3) — frontend dùng khi nối API thật
function idOf(mockId: string): string {
  const h = createHash('md5').update('jpcenter-seed:' + mockId, 'utf8').digest();
  h[6] = (h[6] & 0x0f) | 0x30;
  h[8] = (h[8] & 0x3f) | 0x80;
  const x = h.toString('hex');
  return `${x.slice(0, 8)}-${x.slice(8, 12)}-${x.slice(12, 16)}-${x.slice(16, 20)}-${x.slice(20)}`;
}
const seedIds = [
  ...MOCK_BRANCH_PROFILES.map((b) => b.id),
  ...MOCK_ROLES.map((r) => r.id),
  ...MOCK_EMPLOYEES.map((e) => e.id),
  ...MOCK_STUDENTS.map((x) => x.id),
  ...MOCK_ENROLLMENTS.map((x) => x.id),
  ...MOCK_LEADS.map((x) => x.id),
  ...PORTAL_ACCOUNTS.map((a) => a.id),
  ...MOCK_COURSES.map((c) => c.id),
  ...MOCK_CURRICULA.map((c) => c.id),
  ...MOCK_ROOMS.map((r) => r.id),
  ...MOCK_CLASSES.map((c) => c.id),
  ...MOCK_SESSIONS.map((x) => x.id),
  ...MOCK_CANCEL_REQUESTS.map((x) => x.id),
  ...MOCK_SUB_INVITES.map((x) => x.id),
  ...MOCK_LEAVE_REQUESTS.map((x) => x.id),
  ...MOCK_PROMOTIONS.map((x) => x.id),
  ...MOCK_EVALUATIONS.map((x) => x.id),
  ...MOCK_CARE_LOGS.map((x) => x.id),
  ...MOCK_QUESTIONS.map((x) => x.id),
  ...MOCK_LEAD_RULES.map((x) => x.id),
  ...MOCK_PLACEMENT_TESTS.map((x) => x.id),
  ...MOCK_ATTEMPTS.map((x) => x.id),
  ...MOCK_CAMPAIGNS.map((x) => x.id),
  ...useAppointmentStore.getState().appointments.map((x) => x.id),
  ...useMessageStore.getState().templates.map((x) => x.id),
  ...MOCK_INVOICES.map((x) => x.id),
  ...fin.MOCK_PROMOTIONS.map((x) => x.id),
  ...fin.MOCK_INSTALLMENTS.map((x) => x.id),
  ...fin.MOCK_BANK_TXNS.map((x) => x.id),
  ...learning.academic.assignments.map((x) => x.id),
  ...learning.academic.submissions.map((x) => x.id),
  ...learning.academic.examResults.map((x) => x.id),
  ...learning.academic.announcements.map((x) => x.id),
  ...learning.academic.teacherReviews.map((x) => x.id),
  ...learning.academic.decks.flatMap((d) => [d.id, ...d.cards.map((c) => c.id)]),
  ...learning.academic.cardProgress.map((x) => x.id),
  ...learning.library.teacherDocs.map((x) => x.id),
  ...hrWork.hr.requests.map((x) => x.id),
  ...hrWork.hr.jobs.map((x) => x.id),
  ...hrWork.hr.candidates.map((x) => x.id),
  ...hrWork.hr.onboardings.map((x) => x.id),
  ...hrWork.work.shifts.map((x) => x.id),
  ...hrWork.work.tasks.map((x) => x.id),
  ...hrWork.work.handovers.map((x) => x.id),
];
const idPath = resolve(process.cwd(), 'src', 'lib', 'api', 'seedIds.generated.ts');
mkdirSync(dirname(idPath), { recursive: true });
writeFileSync(
  idPath,
  `// TỰ SINH bởi \`npm run export-seed\` — không sửa tay. Id mock → GUID dữ liệu mẫu backend.
` +
    `export const SEED_IDS: Record<string, string> = ${JSON.stringify(Object.fromEntries([...new Set(seedIds)].map((id) => [id, idOf(id)])), null, 2)};
`,
  'utf8',
);
console.log('✓', idPath);

