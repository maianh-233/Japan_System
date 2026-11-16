import { useMemo } from 'react';
import dayjs from 'dayjs';
import { useClassStore } from '@/features/academic/classes/classStore';
import { teacherName } from '@/features/academic/classes/constants';
import type { ClassInfo, ClassSession } from '@/features/academic/classes/types';
import { useCrmStore } from '@/features/crm/crmStore';
import { useHrStore } from '@/features/hr/hrStore';
import type { HrRequest } from '@/features/hr/types';
import { usePortalStore } from '@/features/portal/portalStore';
import type { CancelRequest, LeaveRequest } from '@/features/portal/types';
import { useAuth } from '@/hooks/useAuth';
import { ALL_BRANCHES } from '@/types/auth';

/**
 * HỘP ĐƠN của người đang đăng nhập — gom 3 nguồn:
 *  - cancel: GV xin hủy buổi (Cổng GV)            → ai có quyền Đào tạo › Buổi học, theo chi nhánh
 *  - leave:  HV / PH xin nghỉ học (Cổng HV / PH)  → như trên
 *  - hr:     Đơn từ nhân viên (nghỉ phép, tạm ứng…) → chỉ đơn mà BƯỚC DUYỆT hiện tại là mình
 */

export type InboxKind = 'cancel' | 'leave' | 'hr';

interface Base {
  id: string;
  status: 'pending' | 'approved' | 'rejected' | 'cancelled';
  at: string;
  /** Người gửi */
  who: string;
  decidedBy?: string;
  decidedAt?: string;
  note?: string;
  /** Có quyền duyệt / từ chối */
  canDecide: boolean;
  branchId?: string;
}
export type InboxItem =
  | (Base & { kind: 'cancel'; req: CancelRequest; session?: ClassSession; cls?: ClassInfo })
  | (Base & { kind: 'leave'; req: LeaveRequest; session?: ClassSession; cls?: ClassInfo; makeup?: ClassSession; makeupCls?: ClassInfo })
  | (Base & { kind: 'hr'; req: HrRequest; step?: { index: number; total: number; role: string } });

export const INBOX_KIND: Record<InboxKind, string> = { cancel: 'GV xin hủy buổi', leave: 'HV xin nghỉ học', hr: 'Đơn từ nhân viên' };

/** Thời điểm buổi học (để xếp đơn gấp lên đầu) */
export const sessionStart = (s?: ClassSession) => (s ? dayjs(`${s.date}T${s.startTime}`) : undefined);

export function useInbox() {
  const { user, branchId, can } = useAuth();
  const cancelReqs = usePortalStore((s) => s.cancelRequests);
  const leaveReqs = usePortalStore((s) => s.leaveRequests);
  const sessions = useClassStore((s) => s.sessions);
  const classes = useClassStore((s) => s.classes);
  const students = useCrmStore((s) => s.students);
  const hrReqs = useHrStore((s) => s.requests);
  const employees = useHrStore((s) => s.employees);

  return useMemo(() => {
    const academic = can('academic.sessions');
    const inB = (b?: string) => branchId === ALL_BRANCHES || b === branchId;
    const ses = (id?: string) => sessions.find((x) => x.id === id);
    const cl = (id?: string) => classes.find((c) => c.id === id);
    const items: InboxItem[] = [];

    if (academic) {
      cancelReqs.forEach((r) => {
        const cls = cl(r.classId);
        if (!inB(cls?.branchId)) return;
        items.push({ id: r.id, kind: 'cancel', req: r, session: ses(r.sessionId), cls, status: r.status, at: r.createdAt, who: teacherName(r.teacherId), decidedBy: r.decidedBy, decidedAt: r.decidedAt, note: r.note, canDecide: true, branchId: cls?.branchId });
      });
      leaveReqs.forEach((r) => {
        const cls = cl(r.classId);
        if (!inB(cls?.branchId)) return;
        const makeup = ses(r.makeupSessionId);
        items.push({
          id: r.id,
          kind: 'leave',
          req: r,
          session: ses(r.sessionId),
          cls,
          makeup,
          makeupCls: cl(makeup?.classId),
          status: r.status,
          at: r.createdAt,
          who: students.find((s) => s.id === r.studentId)?.fullName ?? r.studentId,
          decidedBy: r.decidedBy,
          decidedAt: r.decidedAt,
          note: r.note,
          canDecide: true,
          branchId: cls?.branchId,
        });
      });
    }

    // Đơn từ nhân viên: chờ MÌNH duyệt, hoặc mình đã duyệt (lịch sử)
    const me = user?.fullName;
    hrReqs.forEach((r) => {
      const idx = r.steps.findIndex((s) => !s.decision);
      const current = idx >= 0 ? r.steps[idx] : undefined;
      const mineNow = r.status === 'pending' && current?.approver === me;
      const mineDone = r.steps.find((s) => s.approver === me && s.decision);
      if (!mineNow && !mineDone) return;
      const emp = employees.find((e) => e.id === r.employeeId);
      items.push({
        id: r.id,
        kind: 'hr',
        req: r,
        // Mình đã duyệt bước của mình → với mình đơn đã xử lý, dù còn chờ cấp sau
        status: mineNow ? 'pending' : mineDone!.decision === 'approved' ? 'approved' : 'rejected',
        at: r.createdAt,
        who: emp?.fullName ?? r.employeeId,
        decidedBy: mineDone ? me : undefined,
        decidedAt: mineDone?.at,
        note: mineDone?.comment,
        canDecide: mineNow,
        branchId: emp?.branchId,
        step: current ? { index: idx + 1, total: r.steps.length, role: current.role } : undefined,
      });
    });

    return items;
  }, [user, branchId, can, cancelReqs, leaveReqs, sessions, classes, students, hrReqs, employees]);
}

/** Đơn chờ xử lý, gấp trước: buổi học sắp diễn ra trước, rồi đơn cũ trước */
export function sortPending(items: InboxItem[]) {
  const key = (i: InboxItem) => ('session' in i && i.session ? sessionStart(i.session)!.valueOf() : dayjs(i.at).add(3, 'day').valueOf());
  return items.filter((i) => i.status === 'pending').sort((a, b) => key(a) - key(b));
}
