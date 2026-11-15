import { useMemo, useState } from 'react';
import { Checkbox, Form } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { CalendarPlus, CheckCheck, FileQuestion, Repeat, Wand2, XCircle } from 'lucide-react';
import { Button, DataTable, DateInput, ModalForm, Select, StatusTag, TimeInput, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { EXAM_KINDS } from '../../curriculum/constants';
import { useCurriculumStore } from '../../curriculum/curriculumStore';
import { useClassStore } from '../classStore';
import { effectiveStart, SESSION_STATUS, totalSessionsOf, WEEKDAYS, weekdayOf } from '../constants';
import { generateSessions } from '../sessionGenerator';
import type { ClassInfo, ClassSession, SessionStatus, Weekday } from '../types';

interface GenValues {
  startDate: Dayjs;
  weekdays: Weekday[];
  startTime: Dayjs;
  endTime: Dayjs;
  skipHolidays: boolean;
}

const atTime = (t: string) => {
  const [h, m] = t.split(':').map(Number);
  return dayjs().hour(h).minute(m).second(0);
};

/** Tab Buổi học: danh sách buổi + tự sinh lịch + đánh dấu đã dạy / hủy / học bù */
export function ClassSessionsTab({ cls }: { cls: ClassInfo }) {
  const { can } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const all = useClassStore((s) => s.sessions);
  const replaceUpcoming = useClassStore((s) => s.replaceUpcomingSessions);
  const updateSession = useClassStore((s) => s.updateSession);
  const addSession = useClassStore((s) => s.addSession);
  const updateClass = useClassStore((s) => s.updateClass);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const activeStudents = useStudentStore((s) => s.enrollments.filter((e) => e.classId === cls.id && e.status === 'studying').length);
  const [filter, setFilter] = useState<SessionStatus | 'upcoming'>();
  const [genOpen, setGenOpen] = useState(false);
  const [makeupFor, setMakeupFor] = useState<ClassSession | null>(null);

  const editable = can('academic.classes') && cls.status !== 'finished' && cls.status !== 'cancelled';
  const sessions = useMemo(() => all.filter((s) => s.classId === cls.id).sort((a, b) => a.date.localeCompare(b.date) || a.no - b.no), [all, cls.id]);
  const total = totalSessionsOf(cls);
  const doneSlots = sessions.filter((s) => s.status === 'completed' || s.status === 'makeup').length;
  const today = dayjs().format('YYYY-MM-DD');
  const data = filter
    ? sessions.filter((s) => (filter === 'upcoming' ? s.status === 'scheduled' : s.status === filter))
    : sessions;

  const lessonNo = (id?: string) => (curriculum ? curriculum.lessons.findIndex((l) => l.id === id) + 1 : 0);
  const contentOf = (s: ClassSession) => {
    if (s.kind === 'exam') {
      const exam = curriculum?.exams.find((e) => e.id === s.examId);
      const meta = exam ? EXAM_KINDS[exam.kind] : undefined;
      return meta ? (
        <StatusTag tone={meta.tone} icon={meta.icon} size="sm">
          {exam!.title}
        </StatusTag>
      ) : (
        <StatusTag tone="warning" icon={FileQuestion} size="sm">
          Buổi thi
        </StatusTag>
      );
    }
    const lesson = curriculum?.lessons.find((l) => l.id === s.lessonId);
    return lesson ? (
      <span className="text-[13px]">
        <span className="text-ink-muted">Bài {lessonNo(s.lessonId)}:</span> {lesson.title}
      </span>
    ) : (
      <span className="text-[13px] text-ink-muted">Chưa gán bài</span>
    );
  };

  const markDone = (s: ClassSession) => {
    updateSession(s.id, { status: s.status === 'makeup' ? 'makeup' : 'completed', attendance: { present: activeStudents, total: activeStudents } });
    notify.toast.success(`Đã xác nhận buổi ${s.no} — điểm danh chi tiết làm ở Cổng giáo viên`);
  };

  const cancel = async (s: ClassSession) => {
    const res = await confirm({
      title: `Hủy buổi ${s.no} (${fmtDate(s.date)})?`,
      description: 'Buổi bị hủy không tính vào tiến độ và lương giáo viên. Có thể xếp học bù sau.',
      tone: 'warning',
      confirmAction: 'reject',
      confirmText: 'Hủy buổi',
      requireReason: { label: 'Lý do hủy', placeholder: 'VD: GV ốm, mất điện, nghỉ lễ đột xuất...' },
    });
    if (!res) return;
    updateSession(s.id, { status: 'cancelled', note: res.reason });
    notify.info({ title: `Đã hủy buổi ${s.no}`, description: 'Bấm "Xếp học bù" ở dòng buổi đó để thêm buổi bù.' });
  };

  const columns: DataColumn<ClassSession>[] = [
    { key: 'no', title: 'Buổi', width: 70, align: 'center', render: (_, s) => <span className="tabular font-medium">{s.no}</span> },
    {
      key: 'date',
      title: 'Ngày',
      width: 150,
      mobile: 'title',
      render: (_, s) => (
        <span className={cn('tabular', s.date === today && 'font-semibold text-primary-700')}>
          {WEEKDAYS[weekdayOf(dayjs(s.date)) - 1].short}, {fmtDate(s.date)}
          {s.date === today && ' · Hôm nay'}
        </span>
      ),
    },
    { key: 'time', title: 'Giờ', width: 140, mobile: 'subtitle', render: (_, s) => <span className="tabular">{s.startTime}–{s.endTime}</span> },
    { key: 'content', title: 'Nội dung', width: 260, render: (_, s) => contentOf(s) },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 150,
      mobile: 'aside',
      render: (_, s) => {
        const m = SESSION_STATUS[s.status];
        const overdue = s.status === 'scheduled' && s.date < today;
        return (
          <span className="flex flex-col items-start gap-0.5">
            <StatusTag group="session" status={s.status === 'scheduled' ? 'scheduled' : s.status} size="sm" />
            {overdue && <span className="text-xs text-warning-700">Chưa xác nhận</span>}
            <span className="sr-only">{m.label}</span>
          </span>
        );
      },
    },
    {
      key: 'attendance',
      title: 'Điểm danh',
      width: 100,
      align: 'center',
      render: (_, s) =>
        s.attendance ? (
          <span className={cn('tabular text-[13px]', s.attendance.present < s.attendance.total && 'text-warning-700')}>
            {s.attendance.present}/{s.attendance.total}
          </span>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
    {
      key: 'note',
      title: 'Ghi chú / nhật ký GV',
      width: 240,
      render: (_, s) => (
        <span className="line-clamp-2 text-[13px] text-ink-secondary" title={s.teacherLog ? `${s.teacherLog.content}${s.teacherLog.issues ? `
⚠ ${s.teacherLog.issues}` : ''}` : s.note}>
          {s.note ?? s.teacherLog?.content ?? ((s.status === 'completed' || s.status === 'makeup') && s.attendance ? <span className="text-warning-700">Chưa ghi nhật ký</span> : '')}
        </span>
      ),
    },
  ];

  const nextStart = (() => {
    const lastDone = [...sessions].reverse().find((s) => s.status !== 'scheduled');
    const base = lastDone ? dayjs(lastDone.date).add(1, 'day') : dayjs(effectiveStart(cls));
    return base.isBefore(dayjs(), 'day') && cls.status !== 'enrolling' ? dayjs() : base;
  })();

  return (
    <div className="flex flex-col gap-4">
      {!sessions.length && (
        <div className="flex flex-wrap items-center gap-3 rounded-[var(--radius-card)] border border-primary-200 bg-primary-50 px-4 py-3">
          <Wand2 size={20} className="shrink-0 text-primary-600" aria-hidden />
          <p className="min-w-0 flex-1 text-[13px] text-primary-800">
            Lớp chưa có lịch. Tự sinh <strong>{total} buổi</strong> theo lịch {cls.weekdays.map((d) => WEEKDAYS[d - 1].short).join('-')} · {cls.startTime}–{cls.endTime}, bỏ ngày
            lễ, gán bài học theo lộ trình và chèn buổi thi.
          </p>
          {editable && (
            <Button icon={Wand2} onClick={() => setGenOpen(true)}>
              Tạo lịch tự động
            </Button>
          )}
        </div>
      )}

      <DataTable<ClassSession>
        storageKey="class-sessions"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="buổi"
        searchKeys={['date', 'note']}
        search={{
          placeholder: 'Tìm theo ngày (yyyy-mm-dd), ghi chú...',
          activeFilterCount: filter ? 1 : 0,
          filters: (
            <Select
              placeholder="Trạng thái"
              value={filter}
              onChange={setFilter}
              showSearch={false}
              options={[{ value: 'upcoming', label: 'Sắp diễn ra' }, ...(['completed', 'cancelled', 'makeup'] as SessionStatus[]).map((k) => ({ value: k, label: SESSION_STATUS[k].label }))]}
              className="lg:!w-40"
            />
          ),
          onReset: () => setFilter(undefined),
          actions: (
            <>
              <span className="tabular hidden text-[13px] text-ink-muted lg:inline">
                Đã dạy {doneSlots}/{total}
              </span>
              {editable && sessions.length > 0 && (
                <Button icon={Wand2} variant="outline" collapseOnMobile onClick={() => setGenOpen(true)}>
                  Tạo lại lịch
                </Button>
              )}
            </>
          ),
        }}
        rowActions={
          editable
            ? (s) => (
                <>
                  {(s.status === 'scheduled' || (s.status === 'makeup' && !s.attendance)) && (
                    <Button icon={CheckCheck} iconOnly size="sm" tone="success" variant="soft" tooltip="Xác nhận đã dạy" aria-label="Xác nhận đã dạy" disabled={s.date > today} onClick={() => markDone(s)} />
                  )}
                  {s.status === 'scheduled' && <Button icon={XCircle} iconOnly size="sm" tone="danger" variant="soft" tooltip="Hủy buổi" aria-label="Hủy buổi" onClick={() => cancel(s)} />}
                  {s.status === 'cancelled' && !sessions.some((x) => x.note === `Học bù cho buổi ${s.no}`) && (
                    <Button icon={Repeat} size="sm" tone="warning" variant="soft" onClick={() => setMakeupFor(s)}>
                      Xếp học bù
                    </Button>
                  )}
                </>
              )
            : undefined
        }
        actionsWidth={130}
        empty={{ title: 'Chưa có buổi học', description: 'Bấm "Tạo lịch tự động" để sinh buổi học theo lịch của lớp.' }}
      />

      {/* ===== Tự sinh lịch ===== */}
      <ModalForm<GenValues>
        open={genOpen}
        onClose={() => setGenOpen(false)}
        columns={2}
        width={760}
        title="Tạo lịch tự động"
        description={
          sessions.length
            ? `Giữ nguyên ${sessions.filter((s) => s.status !== 'scheduled').length} buổi đã dạy / hủy / học bù — sinh lại ${Math.max(0, total - doneSlots)} buổi còn lại`
            : `Sinh ${total} buổi (${cls.totalHours} giờ ÷ ${cls.startTime}–${cls.endTime})`
        }
        submitAction="save"
        submitText="Tạo lịch"
        successMessage="Đã tạo lịch học"
        initialValues={{ startDate: nextStart, weekdays: cls.weekdays, startTime: atTime(cls.startTime), endTime: atTime(cls.endTime), skipHolidays: true }}
        onSubmit={(v) => {
          const plan = generateSessions({
            classId: cls.id,
            startDate: v.startDate.format('YYYY-MM-DD'),
            weekdays: v.weekdays,
            startTime: v.startTime.format('HH:mm'),
            endTime: v.endTime.format('HH:mm'),
            count: Math.max(0, total - doneSlots),
            skipHolidays: v.skipHolidays,
            curriculum,
            startNo: sessions.filter((s) => s.status !== 'scheduled').length + 1,
            skipSlots: doneSlots,
          });
          replaceUpcoming(cls.id, plan.sessions);
          if (plan.endDate) updateClass(cls.id, { plannedEndDate: cls.status === 'enrolling' || !cls.actualEndDate ? plan.endDate : cls.plannedEndDate });
        }}
      >
        <Form.Item name="startDate" label="Bắt đầu từ ngày" rules={[{ required: true, message: 'Chọn ngày' }]}>
          <DateInput />
        </Form.Item>
        <div className="grid grid-cols-2 gap-x-3">
          <Form.Item name="startTime" label="Giờ bắt đầu" rules={[{ required: true }]}>
            <TimeInput minuteStep={15} />
          </Form.Item>
          <Form.Item name="endTime" label="Giờ kết thúc" rules={[{ required: true }]}>
            <TimeInput minuteStep={15} />
          </Form.Item>
        </div>
        <Form.Item name="weekdays" label="Học vào các ngày" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn ít nhất 1 ngày' }]} className="md:col-span-2">
          <Checkbox.Group options={WEEKDAYS.map((d) => ({ value: d.value, label: d.label }))} />
        </Form.Item>
        <Form.Item name="skipHolidays" valuePropName="checked" className="md:col-span-2">
          <Checkbox>Bỏ qua ngày nghỉ lễ (Tết, 30/4, 1/5, 2/9...)</Checkbox>
        </Form.Item>
        <Form.Item noStyle shouldUpdate>
          {({ getFieldValue }) => {
            const start = getFieldValue('startDate') as Dayjs | undefined;
            const days = (getFieldValue('weekdays') as Weekday[]) ?? [];
            const st = getFieldValue('startTime') as Dayjs | undefined;
            const et = getFieldValue('endTime') as Dayjs | undefined;
            if (!start || !days.length || !st || !et) return null;
            const plan = generateSessions({
              classId: 'preview',
              startDate: start.format('YYYY-MM-DD'),
              weekdays: days,
              startTime: st.format('HH:mm'),
              endTime: et.format('HH:mm'),
              count: Math.max(0, total - doneSlots),
              skipHolidays: getFieldValue('skipHolidays'),
              curriculum,
              skipSlots: doneSlots,
            });
            return (
              <div className="mb-2 flex flex-col gap-1.5 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2.5 text-[13px] text-primary-800 md:col-span-2">
                <p>
                  <strong>{plan.sessions.length} buổi</strong> · từ {fmtDate(plan.sessions[0]?.date)} đến <strong>{fmtDate(plan.endDate)}</strong>
                  {curriculum && ` · ${plan.sessions.filter((s) => s.kind === 'exam').length} buổi thi theo lộ trình`}
                </p>
                {plan.skipped.length > 0 && <p className="text-primary-700">Bỏ qua: {plan.skipped.map((h) => `${fmtDate(h.date)} (${h.name})`).join(', ')}</p>}
                {!curriculum && <p className="text-warning-700">Lớp chưa chọn lộ trình — buổi học chưa được gán bài.</p>}
              </div>
            );
          }}
        </Form.Item>
      </ModalForm>

      {/* ===== Học bù ===== */}
      <ModalForm<{ date: Dayjs; startTime: Dayjs; endTime: Dayjs }>
        key={makeupFor?.id ?? 'makeup'}
        open={Boolean(makeupFor)}
        onClose={() => setMakeupFor(null)}
        title={`Xếp học bù cho buổi ${makeupFor?.no}`}
        description={makeupFor ? `Buổi ${fmtDate(makeupFor.date)} bị hủy${makeupFor.note ? `: ${makeupFor.note}` : ''}` : undefined}
        submitAction="create"
        submitText="Thêm buổi học bù"
        successMessage="Đã xếp học bù"
        initialValues={makeupFor ? { date: dayjs().add(2, 'day'), startTime: atTime(cls.startTime), endTime: atTime(cls.endTime) } : undefined}
        onSubmit={(v) => {
          if (!makeupFor) return;
          addSession({
            ...makeupFor,
            id: `${cls.id}-MU-${Date.now()}`,
            date: v.date.format('YYYY-MM-DD'),
            startTime: v.startTime.format('HH:mm'),
            endTime: v.endTime.format('HH:mm'),
            status: 'makeup',
            note: `Học bù cho buổi ${makeupFor.no}`,
            attendance: undefined,
          });
        }}
      >
        <Form.Item name="date" label="Ngày học bù" rules={[{ required: true, message: 'Chọn ngày' }]}>
          <DateInput />
        </Form.Item>
        <div className="grid grid-cols-2 gap-x-3">
          <Form.Item name="startTime" label="Từ" rules={[{ required: true }]}>
            <TimeInput minuteStep={15} />
          </Form.Item>
          <Form.Item name="endTime" label="Đến" rules={[{ required: true }]}>
            <TimeInput minuteStep={15} />
          </Form.Item>
        </div>
        <p className="flex items-center gap-1.5 text-xs text-ink-muted">
          <CalendarPlus size={14} aria-hidden /> Hệ thống sẽ kiểm tra trùng lịch phòng & giáo viên khi có module Xếp lịch.
        </p>
      </ModalForm>
      {confirmDialog}
    </div>
  );
}
