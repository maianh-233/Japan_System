import { useMemo } from 'react';
import { Checkbox, Form, Radio } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { TriangleAlert, CircleCheck } from 'lucide-react';
import { DateInput, ModalForm, Select, TextArea, TimeInput, useNotify } from '@/components/common';
import { useStudentStore } from '@/features/student/studentStore';
import { fmtDate } from '@/lib/date';
import { useCurriculumStore } from '../../curriculum/curriculumStore';
import { useClassStore } from '../classStore';
import { TEACHERS } from '../constants';
import { useRoomOptions } from '../../rooms/roomStore';
import { findConflicts, originOf, SESSION_ORIGIN, sessionRoom, sessionTeachers } from '../sessionUtils';
import type { ClassInfo, ClassSession } from '../types';

/** create: tạo buổi học bù / học thêm · edit: dời lịch, đổi GV dạy thay, đổi phòng */
export type ScheduleMode = { kind: 'create'; origin?: 'makeup' | 'extra'; makeupForId?: string } | { kind: 'edit'; session: ClassSession };

interface Values {
  origin: 'makeup' | 'extra';
  makeupForId?: string;
  lessonId?: string;
  date: Dayjs;
  startTime: Dayjs;
  endTime: Dayjs;
  teacherIds: string[];
  room?: string;
  reason?: string;
  notify: boolean;
}

const atTime = (t: string) => {
  const [h, m] = t.split(':').map(Number);
  return dayjs().hour(h).minute(m).second(0);
};
const sameList = (a: string[], b: string[]) => a.length === b.length && a.every((x) => b.includes(x));

export function SessionScheduleModal({ cls, mode, onClose }: { cls: ClassInfo; mode: ScheduleMode | null; onClose: () => void }) {
  const notify = useNotify();
  const [form] = Form.useForm<Values>();
  const allSessions = useClassStore((s) => s.sessions);
  const classes = useClassStore((s) => s.classes);
  const addSession = useClassStore((s) => s.addSession);
  const updateSession = useClassStore((s) => s.updateSession);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const studentCount = useStudentStore((s) => s.enrollments.filter((e) => e.classId === cls.id && e.status === 'studying').length);

  const sessions = useMemo(() => allSessions.filter((s) => s.classId === cls.id), [allSessions, cls.id]);
  /** Buổi bị hủy chưa được xếp bù */
  const cancelledOpen = sessions.filter((s) => s.status === 'cancelled' && originOf(s) === 'regular' && !sessions.some((x) => x.makeupForId === s.id && x.status !== 'cancelled'));
  const editing = mode?.kind === 'edit' ? mode.session : undefined;

  const teacherOptions = TEACHERS.filter((t) => t.branchIds.includes(cls.branchId) || cls.teacherIds.includes(t.id)).map((t) => ({
    value: t.id,
    label: `${t.name}${t.type === 'jp' ? ' (GV Nhật)' : ''}${cls.teacherIds.includes(t.id) ? ' · GV lớp' : ''}`,
  }));
  const roomOptions = useRoomOptions(cls.branchId, editing ? sessionRoom(editing, cls) : cls.room);
  const lessonOptions = (curriculum?.lessons ?? []).map((l, i) => ({ value: l.id, label: `Bài ${i + 1}: ${l.title}` }));

  /** Gợi ý bài cho buổi học thêm: bài đầu tiên chưa dạy xong */
  const suggestLesson = () => {
    const done = new Set(sessions.filter((s) => s.status === 'completed' || s.attendance).map((s) => s.lessonId));
    return curriculum?.lessons.find((l) => !done.has(l.id))?.id;
  };

  const initialValues: Partial<Values> | undefined = !mode
    ? undefined
    : mode.kind === 'edit'
      ? {
          date: dayjs(mode.session.date),
          startTime: atTime(mode.session.startTime),
          endTime: atTime(mode.session.endTime),
          teacherIds: sessionTeachers(mode.session, cls),
          room: sessionRoom(mode.session, cls),
          notify: true,
        }
      : {
          origin: mode.origin ?? (cancelledOpen.length ? 'makeup' : 'extra'),
          makeupForId: mode.makeupForId ?? cancelledOpen[0]?.id,
          lessonId: suggestLesson(),
          date: dayjs().add(2, 'day'),
          startTime: atTime(cls.startTime),
          endTime: atTime(cls.endTime),
          teacherIds: cls.teacherIds,
          room: cls.online ? undefined : cls.room,
          notify: true,
        };

  const submit = (v: Values) => {
    const date = v.date.format('YYYY-MM-DD');
    const startTime = v.startTime.format('HH:mm');
    const endTime = v.endTime.format('HH:mm');
    const conflicts = findConflicts({ id: editing?.id, classId: cls.id, date, startTime, endTime, teacherIds: v.teacherIds, room: v.room }, allSessions, classes);
    if (conflicts.some((c) => c.startsWith('Giờ kết thúc'))) throw new Error('Giờ kết thúc phải sau giờ bắt đầu');
    const overrides = {
      teacherIds: sameList(v.teacherIds, cls.teacherIds) ? undefined : v.teacherIds,
      room: !v.room || v.room === cls.room ? undefined : v.room,
    };

    if (editing) {
      const moved = date !== editing.date || startTime !== editing.startTime || endTime !== editing.endTime;
      updateSession(editing.id, {
        date,
        startTime,
        endTime,
        ...overrides,
        rescheduledFrom: moved ? (editing.rescheduledFrom ?? { date: editing.date, startTime: editing.startTime, endTime: editing.endTime, reason: v.reason }) : editing.rescheduledFrom,
        note: v.reason?.trim() || editing.note,
      });
    } else if (v.origin === 'makeup') {
      const orig = sessions.find((s) => s.id === v.makeupForId);
      if (!orig) throw new Error('Chọn buổi cần bù');
      addSession({
        id: `${cls.id}-MU-${Date.now()}`,
        classId: cls.id,
        no: orig.no,
        kind: orig.kind,
        lessonId: orig.lessonId,
        examId: orig.examId,
        date,
        startTime,
        endTime,
        status: 'makeup',
        origin: 'makeup',
        makeupForId: orig.id,
        note: `Học bù cho buổi ${orig.no}${v.reason?.trim() ? ` — ${v.reason.trim()}` : ''}`,
        ...overrides,
      });
    } else {
      addSession({
        id: `${cls.id}-EX-${Date.now()}`,
        classId: cls.id,
        no: 0,
        kind: 'lesson',
        lessonId: v.lessonId,
        date,
        startTime,
        endTime,
        status: 'scheduled',
        origin: 'extra',
        note: v.reason?.trim() || 'Học thêm để kịp tiến độ',
        ...overrides,
      });
    }
    if (v.notify && studentCount) notify.toast.info(`Đã gửi thông báo lịch học tới ${studentCount} học viên`);
  };

  return (
    <ModalForm<Values>
      key={editing?.id ?? `create-${mode?.kind === 'create' ? (mode.makeupForId ?? mode.origin ?? '') : ''}`}
      form={form}
      open={Boolean(mode)}
      onClose={onClose}
      columns={2}
      width={720}
      title={editing ? `Đổi lịch buổi ${editing.no || '(học thêm)'}` : 'Tạo buổi học'}
      description={
        editing
          ? `Hiện tại: ${fmtDate(editing.date)} · ${editing.startTime}–${editing.endTime}. Dời ngày/giờ, đổi giáo viên dạy thay hoặc đổi phòng.`
          : 'Thêm buổi học bù cho buổi bị hủy, hoặc buổi học thêm để kịp tiến độ.'
      }
      submitAction={editing ? 'save' : 'create'}
      submitText={editing ? 'Lưu thay đổi' : 'Tạo buổi học'}
      successMessage={editing ? 'Đã cập nhật buổi học' : 'Đã tạo buổi học'}
      initialValues={initialValues}
      onSubmit={submit}
    >
      {!editing && (
        <>
          <Form.Item name="origin" label="Loại buổi" className="md:col-span-2">
            <Radio.Group
              optionType="button"
              buttonStyle="solid"
              options={[
                { value: 'makeup', label: `Học bù${cancelledOpen.length ? ` (${cancelledOpen.length} buổi chờ bù)` : ''}`, disabled: !cancelledOpen.length },
                { value: 'extra', label: 'Học thêm' },
              ]}
            />
          </Form.Item>
          <Form.Item noStyle shouldUpdate={(a, b) => a.origin !== b.origin}>
            {({ getFieldValue }) => {
              const origin = getFieldValue('origin') as Values['origin'];
              return (
                <>
                  <p className="-mt-3 mb-3 text-xs text-ink-muted md:col-span-2">{SESSION_ORIGIN[origin ?? 'extra'].hint}</p>
                  {origin === 'makeup' ? (
                    <Form.Item name="makeupForId" label="Bù cho buổi" rules={[{ required: true, message: 'Chọn buổi bị hủy' }]} className="md:col-span-2">
                      <Select
                        placeholder="Chọn buổi bị hủy"
                        options={cancelledOpen.map((s) => ({ value: s.id, label: `Buổi ${s.no} · ${fmtDate(s.date)}${s.note ? ` — ${s.note}` : ''}` }))}
                      />
                    </Form.Item>
                  ) : (
                    <Form.Item name="lessonId" label="Nội dung (bài học)" className="md:col-span-2" extra={curriculum ? 'Gợi ý: bài đầu tiên chưa dạy xong' : 'Lớp chưa có lộ trình'}>
                      <Select allowClear placeholder="Ôn tập / luyện đề (không gắn bài)" options={lessonOptions} />
                    </Form.Item>
                  )}
                </>
              );
            }}
          </Form.Item>
        </>
      )}

      <Form.Item name="date" label="Ngày học" rules={[{ required: true, message: 'Chọn ngày' }]}>
        <DateInput disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
      </Form.Item>
      <div className="grid grid-cols-2 gap-x-3">
        <Form.Item name="startTime" label="Từ" rules={[{ required: true, message: 'Chọn giờ' }]}>
          <TimeInput minuteStep={15} />
        </Form.Item>
        <Form.Item name="endTime" label="Đến" rules={[{ required: true, message: 'Chọn giờ' }]}>
          <TimeInput minuteStep={15} />
        </Form.Item>
      </div>
      <Form.Item name="teacherIds" label="Giáo viên" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn giáo viên' }]} extra="Đổi GV ở đây = dạy thay riêng buổi này">
        <Select mode="multiple" placeholder="Chọn giáo viên" options={teacherOptions} />
      </Form.Item>
      {cls.online ? (
        <Form.Item label="Phòng">
          <p className="text-[13px] text-ink-secondary">Lớp online — dùng link họp của lớp</p>
        </Form.Item>
      ) : (
        <Form.Item name="room" label="Phòng" rules={[{ required: true, message: 'Chọn phòng' }]}>
          <Select placeholder="Chọn phòng" options={roomOptions} />
        </Form.Item>
      )}
      <Form.Item name="reason" label={editing ? 'Lý do thay đổi' : 'Ghi chú'} className="md:col-span-2" rules={editing ? [{ required: true, message: 'Nhập lý do để báo học viên' }] : undefined}>
        <TextArea rows={2} placeholder={editing ? 'VD: GV bận đột xuất, phòng sửa máy chiếu...' : 'VD: Tăng cường trước thi giữa kỳ'} />
      </Form.Item>

      <Form.Item noStyle shouldUpdate>
        {({ getFieldValue }) => {
          const d = getFieldValue('date') as Dayjs | undefined;
          const st = getFieldValue('startTime') as Dayjs | undefined;
          const et = getFieldValue('endTime') as Dayjs | undefined;
          if (!d || !st || !et) return null;
          const list = findConflicts(
            { id: editing?.id, classId: cls.id, date: d.format('YYYY-MM-DD'), startTime: st.format('HH:mm'), endTime: et.format('HH:mm'), teacherIds: getFieldValue('teacherIds') ?? [], room: getFieldValue('room') },
            allSessions,
            classes,
          );
          return list.length ? (
            <div className="mb-3 flex flex-col gap-1 rounded-lg border border-warning-100 bg-warning-50 px-3 py-2.5 text-[13px] text-warning-700 md:col-span-2">
              <p className="flex items-center gap-1.5 font-medium">
                <TriangleAlert size={15} aria-hidden /> Phát hiện {list.length} xung đột lịch
              </p>
              <ul className="list-disc pl-5">
                {list.map((x) => (
                  <li key={x}>{x}</li>
                ))}
              </ul>
            </div>
          ) : (
            <p className="mb-3 flex items-center gap-1.5 text-[13px] text-success-700 md:col-span-2">
              <CircleCheck size={15} aria-hidden /> Không trùng lịch giáo viên, phòng học hay ngày lễ
            </p>
          );
        }}
      </Form.Item>

      <Form.Item name="notify" valuePropName="checked" className="md:col-span-2">
        <Checkbox>Gửi thông báo cho {studentCount} học viên đang học (Cổng học viên + Zalo)</Checkbox>
      </Form.Item>
    </ModalForm>
  );
}
