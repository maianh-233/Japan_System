import { useEffect, useMemo } from 'react';
import { Form, Tooltip } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { Lock, Video } from 'lucide-react';
import { DateInput, NumberInput, Select, TextArea, TextInput, TimeInput } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { courseTotalFee, TEACHER_TYPES, totalHours as courseHours } from '../courses/constants';
import { useCourseStore } from '../courses/courseStore';
import { TIERS } from '../curriculum/constants';
import { useCurriculumStore } from '../curriculum/curriculumStore';
import { CODE_PREFIX, ROOMS, TEACHERS, WEEKDAYS } from './constants';
import { useClassStore } from './classStore';
import { generateSessions } from './sessionGenerator';
import type { ClassInfo, Weekday } from './types';

export interface ClassFormValues {
  courseId: string;
  curriculumId?: string;
  branchId: string;
  codeSuffix: string;
  name: string;
  teacherIds: string[];
  room?: string;
  meetingUrl?: string;
  plannedStartDate: Dayjs;
  weekdays: Weekday[];
  startTime: Dayjs;
  endTime: Dayjs;
  totalHours: number;
  minStudents: number;
  maxStudents: number;
  fee: number;
  note?: string;
}

/** Các trường KHÔNG sửa được khi lớp đã qua trạng thái "Chờ khai giảng" */
export const LOCKED_AFTER_START: (keyof ClassFormValues)[] = [
  'courseId',
  'curriculumId',
  'branchId',
  'codeSuffix',
  'plannedStartDate',
  'weekdays',
  'startTime',
  'endTime',
  'totalHours',
  'minStudents',
  'fee',
];

/** Chọn thứ trong tuần dạng nút bật/tắt */
function WeekdayPicker({ value = [], onChange, disabled }: { value?: Weekday[]; onChange?: (v: Weekday[]) => void; disabled?: boolean }) {
  return (
    <div className="flex flex-wrap gap-1.5" role="group" aria-label="Thứ trong tuần">
      {WEEKDAYS.map((d) => {
        const on = value.includes(d.value);
        return (
          <button
            key={d.value}
            type="button"
            disabled={disabled}
            aria-pressed={on}
            title={d.label}
            onClick={() => onChange?.(on ? value.filter((x) => x !== d.value) : [...value, d.value].sort())}
            className={cn(
              'h-9 min-w-11 rounded-lg border px-2.5 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-60',
              on ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
            )}
          >
            {d.short}
          </button>
        );
      })}
    </div>
  );
}

/** Ô mã lớp: tiền tố cố định (theo khóa) + phần tự nhập */
function CodeInput({ prefix, value, onChange, disabled, id }: { prefix: string; value?: string; onChange?: (v: string) => void; disabled?: boolean; id?: string }) {
  return (
    <div className="flex">
      <span className="inline-flex shrink-0 items-center rounded-l-[var(--radius-control,8px)] border border-r-0 border-line bg-page px-3 font-mono text-[13px] font-semibold text-primary-700">
        {prefix || '—'}
      </span>
      <TextInput id={id} className="!rounded-l-none" value={value} onChange={(e) => onChange?.(e.target.value)} disabled={disabled} placeholder="N3-K14" allowClear={false} />
    </div>
  );
}

const LockHint = () => (
  <Tooltip title="Chỉ sửa được khi lớp đang Chờ khai giảng">
    <Lock size={12} className="ml-1 inline text-ink-muted" aria-label="Đã khóa" />
  </Tooltip>
);

interface Props {
  mode: 'create' | 'edit';
  /** Lớp đã khai giảng → khóa các trường quan trọng */
  locked?: boolean;
  /** Lớp đã kết thúc / hủy → chỉ sửa ghi chú */
  closed?: boolean;
  editingId?: string;
}

/** Ô nhập thông tin lớp. Khi tạo: chọn khóa → tự điền tiền tố mã, học phí, tổng giờ, lộ trình mặc định. */
export function ClassFormFields({ mode, locked = false, closed = false, editingId }: Props) {
  const form = Form.useFormInstance<ClassFormValues>();
  const { branches } = useAuth();
  const courses = useCourseStore((s) => s.courses);
  const curricula = useCurriculumStore((s) => s.curricula);
  const links = useCurriculumStore((s) => s.links);
  const isCodeTaken = useClassStore((s) => s.isCodeTaken);

  const courseId = Form.useWatch('courseId', form);
  const branchId = Form.useWatch('branchId', form);
  const course = courses.find((c) => c.id === courseId);
  const prefix = course ? CODE_PREFIX[course.teacherType] : '';
  const lock = (k: keyof ClassFormValues) => (closed ? k !== 'note' : locked && LOCKED_AFTER_START.includes(k));
  const label = (text: string, k: keyof ClassFormValues) => (
    <span>
      {text}
      {lock(k) && <LockHint />}
    </span>
  );

  // Chọn khóa (khi tạo) → điền mặc định theo khóa
  useEffect(() => {
    if (mode !== 'create' || !course) return;
    const def = links.find((l) => l.courseId === course.id && l.isDefault)?.curriculumId;
    form.setFieldsValue({
      fee: courseTotalFee(course),
      totalHours: courseHours(course),
      curriculumId: def,
      maxStudents: course.format === 'private' ? 1 : (course.maxStudents ?? 20),
      minStudents: course.format === 'private' ? 1 : Math.min(8, course.maxStudents ?? 8),
      teacherIds: [],
    });
  }, [course?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  const curriculumOptions = useMemo(
    () =>
      links
        .filter((l) => l.courseId === courseId)
        .map((l) => ({ link: l, cur: curricula.find((c) => c.id === l.curriculumId) }))
        .filter((x) => x.cur && x.cur.status === 'published')
        .map(({ link, cur }) => ({
          value: cur!.id,
          label: `${cur!.name} ${cur!.version} · ${TIERS[cur!.tier].label}${link.isDefault ? ' (mặc định)' : ''}`,
        })),
    [links, curricula, courseId],
  );

  // Giáo viên đúng loại của khóa (Việt / Nhật / cả hai) + đúng chi nhánh
  const teacherOptions = TEACHERS.filter(
    (t) => (!course || course.teacherType === 'mixed' || t.type === course.teacherType) && (!branchId || t.branchIds.includes(branchId)),
  ).map((t) => ({ value: t.id, label: `${t.name} · GV ${t.type === 'vn' ? 'Việt' : 'Nhật'}` }));

  return (
    <>
      <Form.Item name="courseId" label={label('Khóa học', 'courseId')} rules={[{ required: true, message: 'Chọn khóa học' }]}>
        <Select
          disabled={mode === 'edit'}
          placeholder="Chọn khóa"
          options={courses.filter((c) => c.status === 'active' || c.id === courseId).map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` }))}
        />
      </Form.Item>
      <Form.Item name="branchId" label={label('Chi nhánh', 'branchId')} rules={[{ required: true, message: 'Chọn chi nhánh' }]}>
        <Select
          disabled={mode === 'edit'}
          placeholder="Chọn chi nhánh"
          options={branches.filter((b) => !course || course.branchIds.includes(b.id)).map((b) => ({ value: b.id, label: b.name }))}
        />
      </Form.Item>

      {course && (
        <p className="-mt-2 mb-4 text-xs text-ink-muted md:col-span-2">
          {TEACHER_TYPES[course.teacherType].label} · {course.mode === 'online' ? 'Online' : 'Offline'} · Học phí khóa {formatCurrency(courseTotalFee(course))}
        </p>
      )}

      <Form.Item
        name="codeSuffix"
        label={label('Mã lớp', 'codeSuffix')}
        normalize={(v: string) => v?.toUpperCase().replace(/\s+/g, '-')}
        extra={course ? `Tiền tố ${prefix} theo loại giáo viên của khóa (GVV_ Việt · GVN_ Nhật · GVVN_ Việt + Nhật)` : 'Chọn khóa để có tiền tố mã'}
        rules={[
          { required: true, message: 'Nhập mã lớp' },
          { pattern: /^[A-Z0-9-]+$/, message: 'Chỉ gồm chữ không dấu, số, dấu gạch ngang' },
          { validator: (_, v: string) => (v && isCodeTaken(`${prefix}${v}`, editingId) ? Promise.reject(new Error(`Mã ${prefix}${v} đã tồn tại`)) : Promise.resolve()) },
        ]}
      >
        <CodeInput prefix={prefix} disabled={lock('codeSuffix')} />
      </Form.Item>
      <Form.Item name="name" label="Tên lớp" rules={[{ required: true, whitespace: true, message: 'Nhập tên lớp' }]}>
        <TextInput disabled={lock('name')} placeholder="VD: JLPT N3 K14 — Tối 2-4-6" maxLength={120} />
      </Form.Item>

      <Form.Item name="curriculumId" label={label('Lộ trình', 'curriculumId')} extra={!curriculumOptions.length && courseId ? 'Khóa chưa có lộ trình đã phát hành' : 'Lớp đầu vào tốt → chọn lộ trình Nâng cao'}>
        <Select disabled={lock('curriculumId')} placeholder="Chọn lộ trình" options={curriculumOptions} />
      </Form.Item>
      <Form.Item
        name="teacherIds"
        label="Giáo viên"
        rules={[
          { required: true, type: 'array', min: 1, message: 'Chọn giáo viên' },
          {
            validator: (_, ids: string[] = []) => {
              if (course?.teacherType !== 'mixed') return Promise.resolve();
              const types = new Set(ids.map((id) => TEACHERS.find((t) => t.id === id)?.type));
              return types.has('vn') && types.has('jp') ? Promise.resolve() : Promise.reject(new Error('Lớp Việt + Nhật cần 1 GV Việt và 1 GV Nhật'));
            },
          },
        ]}
      >
        <Select disabled={lock('teacherIds')} mode="multiple" placeholder="Chọn giáo viên" options={teacherOptions} maxCount={course?.teacherType === 'mixed' ? 2 : 1} />
      </Form.Item>

      {course?.mode === 'online' ? (
        <Form.Item name="meetingUrl" label="Link học online" rules={[{ type: 'url', message: 'Link không hợp lệ' }]}>
          <TextInput disabled={lock('meetingUrl')} icon={Video} placeholder="https://zoom.us/j/..." />
        </Form.Item>
      ) : (
        <Form.Item name="room" label="Phòng học">
          <Select disabled={lock('room')} placeholder="Chọn phòng" options={(ROOMS[branchId] ?? []).map((r) => ({ value: r, label: r }))} />
        </Form.Item>
      )}
      <Form.Item name="plannedStartDate" label={label('Ngày khai giảng dự kiến', 'plannedStartDate')} rules={[{ required: true, message: 'Chọn ngày' }]}>
        <DateInput disabled={lock('plannedStartDate')} />
      </Form.Item>

      <Form.Item name="weekdays" label={label('Học vào các ngày', 'weekdays')} rules={[{ required: true, type: 'array', min: 1, message: 'Chọn ít nhất 1 ngày' }]} className="md:col-span-2">
        <WeekdayPicker disabled={lock('weekdays')} />
      </Form.Item>
      <div className="grid grid-cols-2 gap-x-3">
        <Form.Item name="startTime" label={label('Giờ bắt đầu', 'startTime')} rules={[{ required: true, message: 'Chọn giờ' }]}>
          <TimeInput disabled={lock('startTime')} minuteStep={15} />
        </Form.Item>
        <Form.Item
          name="endTime"
          label={label('Giờ kết thúc', 'endTime')}
          dependencies={['startTime']}
          rules={[
            { required: true, message: 'Chọn giờ' },
            ({ getFieldValue }) => ({
              validator: (_, v?: Dayjs) => (v && getFieldValue('startTime') && !v.isAfter(getFieldValue('startTime')) ? Promise.reject(new Error('Phải sau giờ bắt đầu')) : Promise.resolve()),
            }),
          ]}
        >
          <TimeInput disabled={lock('endTime')} minuteStep={15} />
        </Form.Item>
      </div>
      <Form.Item name="totalHours" label={label('Tổng giờ học', 'totalHours')} rules={[{ required: true, message: 'Nhập tổng giờ' }]}>
        <NumberInput disabled={lock('totalHours')} min={1} max={1000} unit="giờ" />
      </Form.Item>

      {/* Tính nhanh: số buổi + ngày kết thúc dự kiến */}
      <Form.Item noStyle shouldUpdate>
        {({ getFieldValue }) => {
          const st = getFieldValue('startTime') as Dayjs | undefined;
          const et = getFieldValue('endTime') as Dayjs | undefined;
          const hours = getFieldValue('totalHours') as number | undefined;
          const days = (getFieldValue('weekdays') as Weekday[] | undefined) ?? [];
          const start = getFieldValue('plannedStartDate') as Dayjs | undefined;
          if (!st || !et || !hours || !et.isAfter(st)) return null;
          const per = et.diff(st, 'minute') / 60;
          const count = Math.ceil(hours / per);
          const end =
            start && days.length
              ? generateSessions({ classId: 'preview', startDate: start.format('YYYY-MM-DD'), weekdays: days, startTime: st.format('HH:mm'), endTime: et.format('HH:mm'), count, skipHolidays: true }).endDate
              : undefined;
          return (
            <p className="mb-5 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-[13px] text-primary-800 md:col-span-2">
              <strong>{count} buổi</strong> × {per.toLocaleString('vi-VN')} giờ/buổi (tổng {hours} giờ ÷ {per.toLocaleString('vi-VN')} giờ)
              {end && (
                <>
                  {' '}
                  · dự kiến kết thúc <strong>{fmtDate(end)}</strong> <span className="text-primary-700">(đã bỏ ngày lễ)</span>
                </>
              )}
            </p>
          );
        }}
      </Form.Item>

      <div className="grid grid-cols-2 gap-x-3">
        <Form.Item name="minStudents" label={label('Sĩ số tối thiểu', 'minStudents')} extra="Đủ mới khai giảng" rules={[{ required: true, message: 'Nhập' }]}>
          <NumberInput disabled={lock('minStudents')} min={1} max={60} unit="HV" />
        </Form.Item>
        <Form.Item
          name="maxStudents"
          label="Sĩ số tối đa"
          dependencies={['minStudents']}
          rules={[
            { required: true, message: 'Nhập' },
            ({ getFieldValue }) => ({ validator: (_, v) => (v && v < getFieldValue('minStudents') ? Promise.reject(new Error('≥ tối thiểu')) : Promise.resolve()) }),
          ]}
        >
          <NumberInput disabled={lock('maxStudents')} min={1} max={60} unit="HV" />
        </Form.Item>
      </div>
      <Form.Item name="fee" label={label('Học phí / học viên', 'fee')} rules={[{ required: true, message: 'Nhập học phí' }]}>
        <NumberInput disabled={lock('fee')} currency min={0} step={100_000} />
      </Form.Item>
      <Form.Item name="note" label="Ghi chú" className="md:col-span-2">
        <TextArea maxLength={500} placeholder="Lý do khai giảng trễ, lưu ý cho giáo viên..." />
      </Form.Item>
    </>
  );
}

// ---------------- Chuyển đổi form ↔ dữ liệu ----------------

/** "18:30" → dayjs hôm nay 18:30 (không cần plugin parse) */
const atTime = (t: string) => {
  const [h, m] = t.split(':').map(Number);
  return dayjs().hour(h).minute(m).second(0).millisecond(0);
};

export function classToForm(c: ClassInfo): ClassFormValues {
  const prefix = c.code.match(/^(GVVN_|GVV_|GVN_)/)?.[1] ?? '';
  return {
    courseId: c.courseId,
    curriculumId: c.curriculumId,
    branchId: c.branchId,
    codeSuffix: c.code.slice(prefix.length),
    name: c.name,
    teacherIds: c.teacherIds,
    room: c.room,
    meetingUrl: c.meetingUrl,
    plannedStartDate: dayjs(c.plannedStartDate),
    weekdays: c.weekdays,
    startTime: atTime(c.startTime),
    endTime: atTime(c.endTime),
    totalHours: c.totalHours,
    minStudents: c.minStudents,
    maxStudents: c.maxStudents,
    fee: c.fee,
    note: c.note,
  };
}

/** Giá trị form → dữ liệu lớp. `prefix` lấy theo khóa. Ngày kết thúc dự kiến tự tính theo lịch. */
export function formToClass(v: ClassFormValues, prefix: string, courseLabel: string, online: boolean) {
  const startTime = v.startTime.format('HH:mm');
  const endTime = v.endTime.format('HH:mm');
  const per = v.endTime.diff(v.startTime, 'minute') / 60;
  const plan = generateSessions({
    classId: 'x',
    startDate: v.plannedStartDate.format('YYYY-MM-DD'),
    weekdays: v.weekdays,
    startTime,
    endTime,
    count: Math.ceil(v.totalHours / per),
    skipHolidays: true,
  });
  return {
    courseId: v.courseId,
    curriculumId: v.curriculumId,
    branchId: v.branchId,
    code: `${prefix}${v.codeSuffix}`,
    name: v.name.trim(),
    courseLabel,
    teacherIds: v.teacherIds,
    room: online ? undefined : v.room,
    online,
    meetingUrl: online ? v.meetingUrl : undefined,
    plannedStartDate: v.plannedStartDate.format('YYYY-MM-DD'),
    plannedEndDate: plan.endDate ?? v.plannedStartDate.format('YYYY-MM-DD'),
    weekdays: v.weekdays,
    startTime,
    endTime,
    totalHours: v.totalHours,
    minStudents: v.minStudents,
    maxStudents: v.maxStudents,
    fee: v.fee,
    note: v.note?.trim() || undefined,
  };
}
