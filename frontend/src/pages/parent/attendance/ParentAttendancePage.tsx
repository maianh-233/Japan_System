import { useState } from 'react';
import { Tabs } from 'antd';
import { CalendarCheck } from 'lucide-react';
import { Button, EmptyState, PageHeader } from '@/components/common';
import { ChildSwitcher } from '@/features/portal/components/ChildSwitcher';
import { AttendanceSection, ClassPills } from '@/features/portal/components/LearningView';
import { LeaveRequestList } from '@/features/portal/components/LeaveRequestList';
import { LeaveRequestModal } from '@/features/portal/components/LeaveRequestModal';
import { useChildren, useStudentId, useStudentLearning } from '@/features/portal/usePortal';

/** Cổng PH › Chuyên cần: điểm danh từng buổi của con + xin nghỉ thay con */
export default function ParentAttendancePage() {
  const studentId = useStudentId();
  const child = useChildren().find((c) => c.id === studentId);
  const list = useStudentLearning(studentId).filter((x) => x.stats && x.enrollment.status !== 'dropped');
  const [classId, setClassId] = useState<string>();
  const [leave, setLeave] = useState(false);
  const cur = list.find((x) => x.cls.id === classId) ?? list[0];

  return (
    <>
      <PageHeader
        title="Chuyên cần"
        subtitle={`${child?.fullName ?? ''} · Trung tâm gửi Zalo cho phụ huynh khi con vắng hoặc đi muộn.`}
        actions={
          <Button action="send" collapseOnMobile onClick={() => setLeave(true)}>
            Xin nghỉ cho con
          </Button>
        }
      />
      <ChildSwitcher />
      <Tabs
        items={[
          {
            key: 'att',
            label: 'Điểm danh',
            children: cur?.stats ? (
              <>
                <ClassPills list={list} value={cur.cls.id} onChange={setClassId} />
                <AttendanceSection stats={cur.stats} />
              </>
            ) : (
              <EmptyState icon={CalendarCheck} title="Chưa có dữ liệu điểm danh" />
            ),
          },
          { key: 'leave', label: 'Đơn xin nghỉ', children: <LeaveRequestList studentId={studentId} /> },
        ]}
      />
      <LeaveRequestModal open={leave} onClose={() => setLeave(false)} studentId={studentId} by="parent" />
    </>
  );
}
