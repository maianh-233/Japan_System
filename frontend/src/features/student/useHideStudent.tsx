import { useConfirm, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { useCrmStore } from '@/features/crm/crmStore';
import type { Student } from '@/features/crm/types';
import { formatCurrency } from '@/lib/format';
import { activeEnrollments, groupByStudent, summarizeTuition, useStudentStore } from './studentStore';
import * as studentApi from './studentApi';
import { USE_API } from '@/lib/api/config';

/**
 * Ẩn / bỏ ẩn học viên.
 * QUY TẮC: chỉ ẩn khi học viên KHÔNG còn ghi danh "Đang học" / "Bảo lưu" ở lớp nào.
 * `hideBlockReason` trả về lý do chặn để UI vô hiệu nút + giải thích.
 */
export function useHideStudent() {
  const { confirm, confirmDialog } = useConfirm();
  const notify = useNotify();
  const setStudentHidden = useCrmStore((s) => s.setStudentHidden);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const classes = useClassStore((s) => s.classes);
  const accounts = useStudentStore((s) => s.accounts);

  const hideBlockReason = (studentId: string): string | null => {
    const active = activeEnrollments(enrollments.filter((e) => e.studentId === studentId));
    if (!active.length) return null;
    const names = active.map((e) => `${classes.find((c) => c.id === e.classId)?.code}${e.status === 'reserved' ? ' (bảo lưu)' : ''}`);
    return `Đang trong lớp ${names.join(', ')} — chỉ ẩn được khi học viên không còn học lớp nào`;
  };

  const toggleHidden = async (student: Student) => {
    if (student.hidden) {
      if (USE_API) {
        try {
          await studentApi.unhideStudent(student.id);
        } catch (e) {
          notify.error({ title: 'Chưa bỏ ẩn được', description: (e as Error).message });
          return;
        }
      } else setStudentHidden(student.id, false);
      notify.toast.success(`Đã bỏ ẩn ${student.fullName}`);
      return;
    }
    const blocked = hideBlockReason(student.id);
    if (blocked) {
      notify.warning({ title: 'Không thể ẩn học viên', description: blocked });
      return;
    }
    const { inv } = groupByStudent(enrollments.filter((e) => e.studentId === student.id), invoices);
    const debt = summarizeTuition(inv.get(student.id) ?? []).remaining;
    const account = accounts.find((a) => a.ownerType === 'student' && a.ownerId === student.id);
    const ok = await confirm({
      title: `Ẩn học viên ${student.fullName}?`,
      description: 'Học viên sẽ không hiện trong danh sách mặc định. Xem lại bằng bộ lọc “Hiện học viên đã ẩn” và bỏ ẩn bất cứ lúc nào.',
      consequences: [
        debt > 0 ? <span className="font-medium text-danger-700">Học viên còn nợ {formatCurrency(debt)} — công nợ vẫn được theo dõi ở Tài chính</span> : 'Không còn nợ học phí',
        account?.status === 'active' ? 'Tài khoản Cổng học viên vẫn hoạt động — khóa riêng ở tab Tài khoản nếu cần' : 'Lịch sử lớp học và học phí được giữ nguyên',
      ],
      tone: 'warning',
      confirmAction: 'save',
      confirmText: 'Ẩn học viên',
      onConfirm: USE_API
        ? async () => {
            // Server kiểm lại "còn học lớp nào" — bị bỏ qua thì báo lý do
            const r = await studentApi.hideStudents([student.id]);
            if (r.skipped.length) throw new Error(r.skipped[0].reason);
          }
        : undefined,
    });
    if (!ok) return;
    if (!USE_API) setStudentHidden(student.id, true);
    notify.success({ title: 'Đã ẩn học viên', description: student.fullName });
  };

  return { toggleHidden, hideBlockReason, confirmDialog };
}
