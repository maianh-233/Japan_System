import { useState } from 'react';
import { Form } from 'antd';
import { Lock } from 'lucide-react';
import { Button, useNotify } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { useCourseStore } from '../../courses/courseStore';
import { useCurriculumStore } from '../../curriculum/curriculumStore';
import { useClassStore } from '../classStore';
import { ClassFormFields, classToForm, formToClass, type ClassFormValues } from '../ClassFormFields';
import { CODE_PREFIX, startDelayDays } from '../constants';
import type { ClassInfo } from '../types';

/**
 * Tab Thông tin lớp: xem + sửa.
 * - Chờ khai giảng: sửa được toàn bộ.
 * - Đang diễn ra / tạm dừng: chỉ sửa tên, giáo viên, phòng / link, sĩ số tối đa, ghi chú.
 * - Kết thúc / hủy: chỉ xem (sửa được ghi chú).
 */
export function ClassOverviewTab({ cls }: { cls: ClassInfo }) {
  const [form] = Form.useForm<ClassFormValues>();
  const notify = useNotify();
  const { can } = useAuth();
  const updateClass = useClassStore((s) => s.updateClass);
  const course = useCourseStore((s) => s.courses.find((c) => c.id === cls.courseId));
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);

  const editable = can('academic.classes');
  const closed = cls.status === 'finished' || cls.status === 'cancelled';
  const locked = cls.status !== 'enrolling';
  const delay = startDelayDays(cls);

  const meta: [string, string][] = [
    ['Ngày khai giảng dự kiến', fmtDate(cls.plannedStartDate)],
    ['Ngày khai giảng thực tế', cls.actualStartDate ? `${fmtDate(cls.actualStartDate)}${delay > 0 ? ` (trễ ${delay} ngày)` : ''}` : 'Chưa khai giảng'],
    ['Ngày kết thúc dự kiến', fmtDate(cls.plannedEndDate)],
    ['Ngày kết thúc thực tế', cls.actualEndDate ? fmtDate(cls.actualEndDate) : '—'],
    ['Lộ trình', curriculum ? `${curriculum.name} ${curriculum.version}` : 'Chưa chọn'],
    ['Ngày mở lớp', fmtDateTime(cls.createdAt)],
  ];

  return (
    <div className="flex flex-col gap-5">
      <dl className="grid grid-cols-1 gap-x-6 gap-y-2 rounded-[var(--radius-card)] bg-page p-4 text-[13px] sm:grid-cols-2">
        {meta.map(([k, v]) => (
          <div key={k} className="flex gap-2">
            <dt className="w-44 shrink-0 text-ink-muted">{k}</dt>
            <dd className="min-w-0 font-medium text-ink">{v}</dd>
          </div>
        ))}
      </dl>

      {locked && editable && (
        <p className="flex items-start gap-2 rounded-lg border border-line bg-page px-3 py-2.5 text-[13px] text-ink-secondary">
          <Lock size={16} className="mt-0.5 shrink-0 text-ink-muted" aria-hidden />
          {closed
            ? 'Lớp đã kết thúc / bị hủy — chỉ sửa được ghi chú.'
            : 'Lớp đã khai giảng — các trường có biểu tượng khóa (mã, khóa, lộ trình, lịch học, giờ học, học phí...) không sửa được. Vẫn sửa được tên lớp, giáo viên, phòng / link học, sĩ số tối đa và ghi chú.'}
        </p>
      )}

      <Form
        form={form}
        layout="vertical"
        requiredMark
        disabled={!editable}
        initialValues={classToForm(cls)}
        onValuesChange={() => setDirty(true)}
        onFinish={async (v) => {
          setSaving(true);
          await new Promise((r) => setTimeout(r, 500)); // giả lập gọi API
          const data = formToClass(v, course ? CODE_PREFIX[course.teacherType] : '', cls.courseLabel, Boolean(cls.online));
          // Lớp đã khai giảng: chỉ ghi các trường được phép
          const patch: Partial<ClassInfo> = closed
            ? { note: data.note }
            : locked
              ? { name: data.name, teacherIds: data.teacherIds, assistantIds: data.assistantIds, room: data.room, meetingUrl: data.meetingUrl, maxStudents: data.maxStudents, note: data.note }
              : data;
          updateClass(cls.id, patch);
          setSaving(false);
          setDirty(false);
          notify.success({ title: 'Đã cập nhật lớp', description: `${data.code} — ${data.name}` });
          if (!locked && patch.weekdays) notify.info({ title: 'Lịch học có thể đã đổi', description: 'Vào tab Buổi học → "Tạo lịch tự động" để sinh lại các buổi.' });
        }}
        className="grid grid-cols-1 gap-x-4 md:grid-cols-2"
      >
        <ClassFormFields mode="edit" locked={locked} closed={closed} editingId={cls.id} />
        {editable && (
          <div className="sticky bottom-0 -mx-4 flex flex-col-reverse gap-2 border-t border-line bg-surface px-4 py-3 sm:-mx-6 sm:flex-row sm:justify-end sm:px-6 md:col-span-2">
            <Button
              action="reset"
              disabled={!dirty || saving}
              onClick={() => {
                form.resetFields();
                setDirty(false);
              }}
            >
              Hoàn tác thay đổi
            </Button>
            <Button action="save" htmlType="submit" loading={saving} disabled={!dirty}>
              Lưu thay đổi
            </Button>
          </div>
        )}
      </Form>
    </div>
  );
}
