import { useMemo, useState, type MouseEvent } from 'react';
import { Checkbox, Form } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { BookOpen, CalendarClock, CalendarPlus, ClipboardCheck, Eye, FileQuestion, Repeat, UserRound, Wand2, XCircle } from 'lucide-react';
import { Button, DataTable, DateInput, ModalForm, Select, StatusTag, TimeInput, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { EXAM_KINDS } from '../../curriculum/constants';
import { useCurriculumStore } from '../../curriculum/curriculumStore';
import { useClassStore } from '../classStore';
import * as classApi from '../classApi';
import { USE_API } from '@/lib/api/config';
import { effectiveStart, SESSION_STATUS, teacherName, totalSessionsOf, WEEKDAYS, weekdayOf } from '../constants';
import { generateSessions } from '../sessionGenerator';
import { canTakeAttendance, isTaught, notStarted, originOf } from '../sessionUtils';
import { SessionDetailDrawer, type SessionTab } from './SessionDetailDrawer';
import { SessionScheduleModal, type ScheduleMode } from './SessionScheduleModal';
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

/** Tab Buổi học: danh sách buổi + tự sinh lịch + chi tiết / điểm danh / đổi lịch / hủy / tạo buổi học bù & học thêm */
export function ClassSessionsTab({ cls }: { cls: ClassInfo }) {
  const { can, user } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const all = useClassStore((s) => s.sessions);
  const replaceUpcoming = useClassStore((s) => s.replaceUpcomingSessions);
  const updateSession = useClassStore((s) => s.updateSession);
  const updateClass = useClassStore((s) => s.updateClass);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const activeStudents = useStudentStore((s) => s.enrollments.filter((e) => e.classId === cls.id && e.status === 'studying').length);
  const [filter, setFilter] = useState<SessionStatus | 'upcoming' | 'extra'>();
  const [genOpen, setGenOpen] = useState(false);
  const [schedule, setSchedule] = useState<ScheduleMode | null>(null);
  const [detail, setDetail] = useState<{ id: string; tab: SessionTab } | null>(null);

  const editable = can('academic.classes') && cls.status !== 'finished' && cls.status !== 'cancelled';
  const sessions = useMemo(() => all.filter((s) => s.classId === cls.id).sort((a, b) => a.date.localeCompare(b.date) || a.no - b.no), [all, cls.id]);
  const total = totalSessionsOf(cls);
  const doneSlots = sessions.filter(isTaught).length;
  const extraCount = sessions.filter((s) => s.origin === 'extra').length;
  const today = dayjs().format('YYYY-MM-DD');
  const data = filter
    ? sessions.filter((s) =>
        filter === 'upcoming' ? s.status === 'scheduled' || (s.status === 'makeup' && !s.attendance) : filter === 'extra' ? s.origin === 'extra' : s.status === filter,
      )
    : sessions;

  const lessonNo = (id?: string) => (curriculum ? curriculum.lessons.findIndex((l) => l.id === id) + 1 : 0);
  /** Buổi bị hủy đã có buổi bù (chưa bị hủy) */
  const hasMakeup = (s: ClassSession) => sessions.some((x) => x.makeupForId === s.id && x.status !== 'cancelled');
  /** Buổi theo lịch bị hủy, chưa có buổi bù */
  const needsMakeup = (s: ClassSession) => s.status === 'cancelled' && originOf(s) === 'regular' && !hasMakeup(s);

  const lessonOf = (s: ClassSession) => {
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
      <span className="text-[13px] text-ink-muted">{s.origin === 'extra' ? 'Ôn tập / luyện đề' : 'Chưa gán bài'}</span>
    );
  };
  const contentOf = (s: ClassSession) => (
    <span className="flex flex-col items-start gap-0.5">
      {lessonOf(s)}
      {(s.teacherIds || s.room) && (
        <span className="flex items-center gap-1 text-xs text-warning-700">
          <UserRound size={12} aria-hidden />
          {[s.teacherIds && `Dạy thay: ${s.teacherIds.map(teacherName).join(', ')}`, s.room && `Phòng ${s.room}`].filter(Boolean).join(' · ')}
        </span>
      )}
    </span>
  );

  const cancel = async (s: ClassSession) => {
    const res = await confirm({
      title: `Hủy ${s.no ? `buổi ${s.no}` : 'buổi học thêm'} (${fmtDate(s.date)} · ${s.startTime})?`,
      consequences: [
        'Không tính vào tiến độ và lương giáo viên',
        activeStudents ? `${activeStudents} học viên nhận thông báo nghỉ học (Cổng học viên + Zalo)` : 'Lớp chưa có học viên',
        s.origin === 'extra'
          ? 'Buổi học thêm — không cần xếp bù'
          : originOf(s) === 'makeup'
            ? `Buổi ${s.no} gốc quay lại trạng thái "Chờ xếp bù"`
            : 'Có thể xếp học bù ngay sau bước này',
      ],
      tone: 'warning',
      confirmAction: 'reject',
      confirmText: 'Hủy buổi',
      requireReason: { label: 'Lý do hủy', placeholder: 'VD: GV ốm, mất điện, nghỉ lễ đột xuất...' },
    });
    if (!res) return;
    if (!notStarted(s)) {
      notify.toast.error('Buổi học đã bắt đầu — không thể hủy');
      return;
    }
    if (USE_API) {
      try {
        await classApi.cancelSession(s.id, res.reason ?? '');
      } catch (e) {
        notify.error({ title: 'Chưa hủy được buổi', description: (e as Error).message });
        return;
      }
    } else updateSession(s.id, { status: 'cancelled', note: res.reason, cancelledBy: { name: user?.fullName ?? 'Giáo vụ', at: new Date().toISOString() } });
    notify.info({
      title: `Đã hủy ${s.no ? `buổi ${s.no}` : 'buổi học thêm'}`,
      description: s.origin === 'extra' ? undefined : 'Bấm nút "Xếp học bù" ở dòng buổi gốc, hoặc "Tạo buổi học".',
    });
  };

  const columns: DataColumn<ClassSession>[] = [
    {
      key: 'no',
      title: 'Buổi',
      width: 70,
      align: 'center',
      render: (_, s) =>
        s.origin === 'extra' ? (
          <span className="inline-flex" title="Buổi học thêm (ngoài số buổi khóa)">
            <BookOpen size={16} className="text-primary-600" aria-label="Học thêm" />
          </span>
        ) : (
          <span className="tabular font-medium">{s.no}</span>
        ),
    },
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
        const overdue = (s.status === 'scheduled' || s.status === 'makeup') && !s.attendance && s.date < today;
        return (
          <span className="flex flex-col items-start gap-0.5">
            {s.origin === 'extra' && s.status !== 'cancelled' ? (
              <StatusTag tone="primary" icon={BookOpen} size="sm">
                {s.attendance ? 'Đã học thêm' : 'Học thêm'}
              </StatusTag>
            ) : (
              <StatusTag group="session" status={s.status} size="sm" />
            )}
            {overdue && <span className="text-xs text-warning-700">Chưa điểm danh</span>}
            {s.rescheduledFrom && <span className="text-xs text-ink-muted">Dời từ {fmtDate(s.rescheduledFrom.date)}</span>}
            {needsMakeup(s) && <span className="text-xs text-warning-700">Chờ xếp bù</span>}
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
          {s.teacherLog?.content ?? s.note ?? ((s.status === 'completed' || s.status === 'makeup') && s.attendance ? <span className="text-warning-700">Chưa ghi nhật ký</span> : '')}
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
              options={[
                { value: 'upcoming', label: 'Sắp diễn ra' },
                ...(['completed', 'cancelled', 'makeup'] as SessionStatus[]).map((k) => ({ value: k, label: SESSION_STATUS[k].label })),
                { value: 'extra', label: 'Học thêm' },
              ]}
              className="lg:!w-40"
            />
          ),
          onReset: () => setFilter(undefined),
          actions: (
            <>
              <span className="tabular hidden text-[13px] text-ink-muted lg:inline">
                Đã dạy {doneSlots}/{total}
                {extraCount > 0 && ` · +${extraCount} học thêm`}
              </span>
              {editable && sessions.length > 0 && (
                <Button icon={CalendarPlus} collapseOnMobile onClick={() => setSchedule({ kind: 'create' })}>
                  Tạo buổi học
                </Button>
              )}
              {editable && sessions.length > 0 && (
                <Button icon={Wand2} variant="outline" collapseOnMobile onClick={() => setGenOpen(true)}>
                  Tạo lại lịch
                </Button>
              )}
            </>
          ),
        }}
        onRowClick={(s) => setDetail({ id: s.id, tab: 'info' })}
        rowActions={(s) => {
          const upcoming = (s.status === 'scheduled' || (s.status === 'makeup' && !s.attendance)) && notStarted(s);
          const stop = (fn: () => void) => (e: MouseEvent) => {
            e.stopPropagation();
            fn();
          };
          return (
            <>
              <Button icon={Eye} iconOnly size="sm" variant="ghost" tooltip="Xem chi tiết" aria-label="Xem chi tiết" onClick={stop(() => setDetail({ id: s.id, tab: 'info' }))} />
              {editable && canTakeAttendance(s) && (
                <Button
                  icon={ClipboardCheck}
                  iconOnly
                  size="sm"
                  tone={s.attendance ? 'neutral' : 'success'}
                  variant="soft"
                  tooltip={s.attendance ? 'Sửa điểm danh' : 'Điểm danh'}
                  aria-label="Điểm danh"
                  onClick={stop(() => setDetail({ id: s.id, tab: 'students' }))}
                />
              )}
              {editable && upcoming && (
                <Button icon={CalendarClock} iconOnly size="sm" variant="soft" tooltip="Đổi lịch / dạy thay" aria-label="Đổi lịch" onClick={stop(() => setSchedule({ kind: 'edit', session: s }))} />
              )}
              {editable && upcoming && (
                <Button icon={XCircle} iconOnly size="sm" tone="danger" variant="soft" tooltip="Hủy buổi" aria-label="Hủy buổi" onClick={stop(() => cancel(s))} />
              )}
              {editable && needsMakeup(s) && (
                <Button
                  icon={Repeat}
                  iconOnly
                  size="sm"
                  tone="warning"
                  variant="soft"
                  tooltip="Xếp học bù"
                  aria-label="Xếp học bù"
                  onClick={stop(() => setSchedule({ kind: 'create', origin: 'makeup', makeupForId: s.id }))}
                />
              )}
            </>
          );
        }}
        actionsWidth={editable ? 150 : 60}
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
        onSubmit={async (v) => {
          if (USE_API) {
            // Server thay buổi chưa dạy (giữ buổi đã dạy / hủy / học bù / học thêm), bỏ ngày lễ trong Cài đặt chung
            const plan = await classApi.generateSchedule(cls.id, {
              from: v.startDate.format('YYYY-MM-DD'),
              weekdays: v.weekdays,
              startTime: v.startTime.format('HH:mm'),
              endTime: v.endTime.format('HH:mm'),
              skipHolidays: v.skipHolidays,
            });
            if (plan.overridesLost) notify.info({ title: `Đã tạo ${plan.count} buổi`, description: `${plan.overridesLost} buổi có thay đổi riêng (dạy thay, đổi phòng, dời lịch) đã được thay theo lịch mới.` });
            return;
          }
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

      {/* ===== Tạo buổi học bù / học thêm · Đổi lịch ===== */}
      <SessionScheduleModal cls={cls} mode={schedule} onClose={() => setSchedule(null)} />

      {/* ===== Chi tiết buổi ===== */}
      <SessionDetailDrawer
        cls={cls}
        sessionId={detail?.id ?? null}
        initialTab={detail?.tab}
        editable={editable}
        onClose={() => setDetail(null)}
        onOpen={(id) => setDetail({ id, tab: 'info' })}
        onReschedule={(s) => setSchedule({ kind: 'edit', session: s })}
        onCancel={cancel}
        onMakeup={(s) => setSchedule({ kind: 'create', origin: 'makeup', makeupForId: s.id })}
      />
      {confirmDialog}
    </div>
  );
}
