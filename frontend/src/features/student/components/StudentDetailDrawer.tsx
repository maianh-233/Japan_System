import { useEffect, useState } from 'react';
import { Drawer, Tabs, Tooltip } from 'antd';
import { Eye, EyeOff, Info, KeyRound, MessageCircle, School, Users } from 'lucide-react';
import { Button, StatusTag } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { SendMessageModal } from '@/features/crm/messages/SendMessageModal';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { STUDY_STATUS } from '../constants';
import { studyStatusOf, useStudentStore } from '../studentStore';
import { useHideStudent } from '../useHideStudent';
import { usePortalAccountActions } from '../portalAccountApi';
import { AccountCard } from './AccountCard';
import { StudentClassesTab } from './StudentClassesTab';
import { StudentGuardiansTab } from './StudentGuardiansTab';
import { StudentInfoTab } from './StudentInfoTab';

export type StudentTab = 'info' | 'account' | 'classes' | 'guardians';

interface Props {
  studentId: string | null;
  initialTab?: StudentTab;
  onClose: () => void;
}

/** Chi tiết học viên: Thông tin (xem + sửa) · Tài khoản · Lớp học & học phí · Người thân */
export function StudentDetailDrawer({ studentId, initialTab = 'info', onClose }: Props) {
  const { isMobile } = useBreakpoint();
  const student = useCrmStore((s) => s.students.find((x) => x.id === studentId));
  const guardianCount = useCrmStore((s) => s.links.filter((k) => k.studentId === studentId && k.relationship !== 'self').length);
  const enrollments = useStudentStore((s) => s.enrollments);
  const account = useStudentStore((s) => s.accounts.find((a) => a.ownerType === 'student' && a.ownerId === studentId));
  const { issue: issueAccount } = usePortalAccountActions();
  const { toggleHidden, hideBlockReason, confirmDialog } = useHideStudent();
  const [tab, setTab] = useState<StudentTab>(initialTab);
  const [messaging, setMessaging] = useState(false);

  useEffect(() => {
    if (studentId) setTab(initialTab);
  }, [studentId, initialTab]);

  const mine = enrollments.filter((e) => e.studentId === studentId);
  const study = STUDY_STATUS[studyStatusOf(mine)];
  const blocked = student && !student.hidden ? hideBlockReason(student.id) : null;

  const label = (Icon: typeof Info, text: string) => (
    <span className="inline-flex items-center gap-1.5">
      <Icon size={16} aria-hidden /> {text}
    </span>
  );

  return (
    <Drawer
      open={Boolean(studentId)}
      onClose={onClose}
      width={isMobile ? '100%' : 780}
      destroyOnHidden
      styles={{ body: { paddingTop: 0 } }}
      title={
        student && (
          <div className="flex min-w-0 flex-col gap-1 pr-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="truncate text-base font-semibold">{student.fullName}</span>
              <StatusTag tone={study.tone} icon={study.icon} size="sm">
                {study.label}
              </StatusTag>
              {student.hidden && (
                <StatusTag tone="neutral" icon={EyeOff} size="sm">
                  Đã ẩn
                </StatusTag>
              )}
            </div>
            <span className="text-xs font-normal text-ink-muted">
              {student.code}
              {student.phone && ` · ${student.phone}`}
            </span>
          </div>
        )
      }
      extra={
        student && (
          <div className="flex gap-2">
          <Button icon={MessageCircle} size="sm" variant="outline" collapseOnMobile onClick={() => setMessaging(true)}>
            Nhắn tin
          </Button>
          <Tooltip title={blocked}>
            <span>
              <Button
                icon={student.hidden ? Eye : EyeOff}
                tone="neutral"
                variant="outline"
                size="sm"
                collapseOnMobile
                disabled={Boolean(blocked)}
                onClick={() => toggleHidden(student)}
              >
                {student.hidden ? 'Bỏ ẩn' : 'Ẩn học viên'}
              </Button>
            </span>
          </Tooltip>
          </div>
        )
      }
    >
      {student && (
        <Tabs
          activeKey={tab}
          onChange={(k) => setTab(k as StudentTab)}
          items={[
            { key: 'info', label: label(Info, 'Thông tin'), children: <StudentInfoTab key={student.id} student={student} /> },
            {
              key: 'account',
              label: label(KeyRound, 'Tài khoản'),
              children: (
                <AccountCard
                  account={account}
                  ownerName={student.fullName}
                  portalLabel="Cổng học viên"
                  emptyHint="Học viên chưa có tài khoản Cổng học viên (xem lịch học, bài tập, học phí)."
                  onIssue={() =>
                    void issueAccount({ ownerType: 'student', ownerId: student.id, username: student.code.toLowerCase(), email: student.email, ownerName: student.fullName })
                  }
                />
              ),
            },
            { key: 'classes', label: label(School, `Lớp học & học phí (${mine.length})`), children: <StudentClassesTab studentId={student.id} /> },
            { key: 'guardians', label: label(Users, `Người thân (${guardianCount})`), children: <StudentGuardiansTab studentId={student.id} /> },
          ]}
        />
      )}
      {confirmDialog}
      <SendMessageModal target={student && messaging ? { kind: 'student', studentId: student.id } : null} onClose={() => setMessaging(false)} />
    </Drawer>
  );
}
