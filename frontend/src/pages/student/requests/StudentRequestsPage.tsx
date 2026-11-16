import { useState } from 'react';
import { Button, PageHeader } from '@/components/common';
import { LeaveRequestList } from '@/features/portal/components/LeaveRequestList';
import { LeaveRequestModal } from '@/features/portal/components/LeaveRequestModal';
import { useStudentId } from '@/features/portal/usePortal';

/** Cổng HV › Đơn xin nghỉ: đơn đã gửi, trạng thái duyệt, buổi học bù đã đăng ký */
export default function StudentRequestsPage() {
  const studentId = useStudentId();
  const [open, setOpen] = useState(false);
  return (
    <>
      <PageHeader
        title="Đơn xin nghỉ"
        subtitle="Xin nghỉ trước giờ học → vắng có phép. Có thể đăng ký học bù ở lớp khác cùng khóa."
        actions={
          <Button action="send" collapseOnMobile onClick={() => setOpen(true)}>
            Xin nghỉ
          </Button>
        }
      />
      <LeaveRequestList studentId={studentId} />
      <LeaveRequestModal open={open} onClose={() => setOpen(false)} studentId={studentId} by="student" />
    </>
  );
}
