import { useMemo, useState } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { Ban } from 'lucide-react';
import { DateInput, ModalForm, PageHeader, Select, TextArea, Timetable, useNotify } from '@/components/common';
import { buildWeekSessions, type MockSession } from '@/mocks/data/schedule';

interface RequestForm {
  reasonType: string;
  reason: string;
  makeupDate?: dayjs.Dayjs;
}

/**
 * Thời khóa biểu cá nhân dùng chung:
 *  - Giáo viên: bấm buổi → gửi ĐƠN HỦY BUỔI (chỉ trung tâm được hủy cả lớp)
 *  - Học viên: bấm buổi → gửi ĐƠN XIN NGHỈ
 */
export function SessionTimetablePage({ role }: { role: 'teacher' | 'student' }) {
  const notify = useNotify();
  const [week, setWeek] = useState(() => dayjs());
  const [selected, setSelected] = useState<MockSession | null>(null);
  const [cancelled, setCancelled] = useState<Set<string | number>>(new Set());

  const events = useMemo(
    () =>
      buildWeekSessions(week).map((s) =>
        cancelled.has(s.id) ? { ...s, subtitle: role === 'teacher' ? 'Đã gửi đơn hủy — chờ duyệt' : 'Đã xin nghỉ — chờ duyệt', tone: 'warning' as const } : s,
      ),
    [week, cancelled, role],
  );

  const isTeacher = role === 'teacher';
  const past = selected ? dayjs(selected.start).isBefore(dayjs()) : false;

  return (
    <>
      <PageHeader
        title="Thời khóa biểu"
        subtitle={isTeacher ? 'Bấm vào một buổi để gửi đơn xin hủy buổi.' : 'Bấm vào một buổi để xin nghỉ buổi đó.'}
      />
      <div className="rounded-[var(--radius-card)] border border-line bg-surface p-3 sm:p-4">
        <Timetable
          events={events}
          week={week}
          onWeekChange={setWeek}
          onEventClick={(e) => {
            const s = events.find((x) => x.id === e.id) ?? null;
            if (s && cancelled.has(s.id)) {
              notify.info({ title: 'Đơn đang chờ duyệt', description: `${s.className} · ${dayjs(s.start).format('HH:mm DD/MM')}` });
              return;
            }
            setSelected(s);
          }}
        />
      </div>

      <ModalForm<RequestForm>
        open={Boolean(selected)}
        onClose={() => setSelected(null)}
        title={isTeacher ? 'Gửi đơn hủy buổi dạy' : 'Gửi đơn xin nghỉ học'}
        description={selected ? `${selected.className} · ${dayjs(selected.start).format('dddd HH:mm, DD/MM/YYYY')} · ${selected.location}` : undefined}
        submitAction="send"
        submitText="Gửi đơn"
        successMessage={isTeacher ? 'Đã gửi đơn hủy buổi — giáo vụ sẽ xử lý' : 'Đã gửi đơn xin nghỉ'}
        onSubmit={async () => {
          if (past) throw new Error('Không thể gửi đơn cho buổi đã diễn ra.');
          await new Promise((r) => setTimeout(r, 700));
          if (selected) setCancelled((s) => new Set(s).add(selected.id));
        }}
      >
        {past && (
          <p className="mb-4 flex items-center gap-2 rounded-lg bg-danger-50 px-3 py-2 text-[13px] text-danger-700">
            <Ban size={16} aria-hidden /> Buổi này đã diễn ra, không thể gửi đơn.
          </p>
        )}
        <Form.Item name="reasonType" label="Lý do" rules={[{ required: true, message: 'Chọn lý do' }]}>
          <Select
            placeholder="Chọn lý do"
            options={(isTeacher
              ? ['Ốm / sức khỏe', 'Việc gia đình đột xuất', 'Sự cố thiết bị / mạng (lớp online)', 'Khác']
              : ['Ốm / sức khỏe', 'Việc gia đình', 'Trùng lịch học / làm', 'Khác']
            ).map((v) => ({ value: v, label: v }))}
          />
        </Form.Item>
        <Form.Item name="reason" label="Chi tiết" rules={[{ required: true, message: 'Nhập chi tiết' }, { min: 10, message: 'Tối thiểu 10 ký tự' }]}>
          <TextArea maxLength={300} placeholder="Mô tả ngắn gọn lý do..." />
        </Form.Item>
        {isTeacher && (
          <Form.Item name="makeupDate" label="Đề xuất ngày dạy bù" extra="Giáo vụ sẽ xếp phòng và báo lại.">
            <DateInput showTime disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
          </Form.Item>
        )}
      </ModalForm>
    </>
  );
}
