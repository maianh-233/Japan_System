import { useMemo } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { ModalForm, Select, TextArea } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { notStarted } from '@/features/academic/classes/sessionUtils';
import { useAuth } from '@/hooks/useAuth';
import { usePortalStore } from '../portalStore';
import { activeOf, shortCode, useStudentLearning } from '../usePortal';

interface Props {
  open: boolean;
  onClose: () => void;
  studentId: string;
  by: 'student' | 'parent';
  /** Mở sẵn cho 1 buổi (bấm từ thời khóa biểu) */
  sessionId?: string;
}

type Values = { sessionId: string; reasonType: string; reason: string; makeupSessionId?: string };

const REASONS = ['Ốm / sức khỏe', 'Việc gia đình', 'Trùng lịch học / làm', 'Đi xa / công tác', 'Khác'];

/**
 * Đơn xin nghỉ 1 buổi (HV hoặc PH gửi thay con). Chỉ xin được buổi CHƯA diễn ra.
 * Có thể đăng ký học bù ở lớp khác CÙNG KHÓA trong 3 tuần tới (giáo vụ duyệt cùng đơn).
 */
export function LeaveRequestModal({ open, onClose, studentId, by, sessionId }: Props) {
  const { user } = useAuth();
  const sessions = useClassStore((s) => s.sessions);
  const classes = useClassStore((s) => s.classes);
  const requests = usePortalStore((s) => s.leaveRequests);
  const add = usePortalStore((s) => s.addLeaveRequest);
  const mine = activeOf(useStudentLearning(studentId));

  const upcoming = useMemo(() => {
    const requested = new Set(requests.filter((r) => r.studentId === studentId).map((r) => r.sessionId));
    return mine
      .flatMap(({ cls }) => sessions.filter((s) => s.classId === cls.id && s.status === 'scheduled' && notStarted(s) && !requested.has(s.id)).map((s) => ({ s, cls })))
      .sort((a, b) => (a.s.date + a.s.startTime).localeCompare(b.s.date + b.s.startTime))
      .slice(0, 20);
  }, [mine, sessions, requests, studentId]);

  return (
    <ModalForm<Values>
      key={sessionId ?? 'leave'}
      open={open}
      onClose={onClose}
      title={by === 'parent' ? 'Xin nghỉ học cho con' : 'Gửi đơn xin nghỉ'}
      description="Gửi trước giờ học. Đơn được duyệt → buổi đó tính “vắng có phép”. Đi học bù đầy đủ → buổi đó tính như có mặt khi xét chuyên cần & cấp chứng nhận."
      submitAction="send"
      submitText="Gửi đơn"
      successMessage="Đã gửi đơn — giáo vụ sẽ duyệt và báo lại"
      initialValues={{ sessionId }}
      onSubmit={(v) => {
        const s = sessions.find((x) => x.id === v.sessionId)!;
        if (!notStarted(s)) throw new Error('Buổi này đã bắt đầu — liên hệ giáo vụ để báo vắng.');
        add({ studentId, classId: s.classId, sessionId: s.id, reasonType: v.reasonType, reason: v.reason, makeupSessionId: v.makeupSessionId, by, byName: user?.fullName ?? '' });
      }}
    >
      <Form.Item name="sessionId" label="Buổi nghỉ" rules={[{ required: true, message: 'Chọn buổi' }]}>
        <Select
          placeholder="Chọn buổi sắp tới"
          options={upcoming.map(({ s, cls }) => ({ value: s.id, label: `${dayjs(s.date).format('dd DD/MM')} ${s.startTime} · ${shortCode(cls)} · buổi ${s.no}${s.kind === 'exam' ? ' (THI)' : ''}` }))}
        />
      </Form.Item>
      <Form.Item name="reasonType" label="Lý do" rules={[{ required: true, message: 'Chọn lý do' }]}>
        <Select options={REASONS.map((v) => ({ value: v, label: v }))} showSearch={false} />
      </Form.Item>
      <Form.Item name="reason" label="Chi tiết" rules={[{ required: true, message: 'Nhập chi tiết' }, { min: 5, message: 'Tối thiểu 5 ký tự' }]}>
        <TextArea rows={2} maxLength={300} />
      </Form.Item>
      <Form.Item noStyle shouldUpdate={(a, b) => a.sessionId !== b.sessionId}>
        {({ getFieldValue }) => {
          const s = sessions.find((x) => x.id === getFieldValue('sessionId'));
          const cls = classes.find((c) => c.id === s?.classId);
          if (!s || !cls) return null;
          if (s.kind === 'exam') return <p className="text-xs text-warning-700">Buổi thi: giáo vụ sẽ xếp lịch thi bù riêng.</p>;
          // Lớp khác cùng khóa, buổi trong 3 tuần tới, KHÔNG trùng giờ với các lớp HV đang học
          const myIds = mine.map((m) => m.cls.id);
          const clash = (x: (typeof sessions)[number]) =>
            sessions.some((o) => myIds.includes(o.classId) && o.id !== s.id && o.status !== 'cancelled' && o.date === x.date && o.startTime < x.endTime && x.startTime < o.endTime);
          const options = sessions
            .filter((x) => x.classId !== cls.id && x.status === 'scheduled' && x.kind === 'lesson' && notStarted(x) && dayjs(x.date).diff(dayjs(), 'day') <= 21 && !clash(x))
            .map((x) => ({ x, c: classes.find((c) => c.id === x.classId) }))
            .filter(({ c }) => c && c.courseId === cls.courseId && c.status === 'ongoing')
            .slice(0, 12);
          return (
            <Form.Item name="makeupSessionId" label="Đăng ký học bù (tùy chọn)" extra={options.length ? 'Học cùng lớp khác cùng khóa — nội dung có thể lệch 1–2 bài. Giáo viên lớp đó điểm danh, có mặt → buổi nghỉ được tính như có mặt.' : 'Không có buổi nào của lớp cùng khóa trong 3 tuần tới mà không trùng lịch học của bạn — giáo vụ sẽ liên hệ xếp học bù.'}>
              <Select
                disabled={!options.length}
                placeholder="Không đăng ký"
                options={options.map(({ x, c }) => ({ value: x.id, label: `${dayjs(x.date).format('dd DD/MM')} ${x.startTime} · ${shortCode(c!)} · buổi ${x.no}${c!.online ? ' · online' : ''}` }))}
              />
            </Form.Item>
          );
        }}
      </Form.Item>
    </ModalForm>
  );
}
