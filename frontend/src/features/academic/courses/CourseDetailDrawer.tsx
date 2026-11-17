import { useEffect, useState } from 'react';
import { Drawer, Form, Tabs } from 'antd';
import { Info, Route } from 'lucide-react';
import { Button, StatusTag, useNotify } from '@/components/common';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { useCurriculumStore } from '../curriculum/curriculumStore';
import { COURSE_STATUS, FORMATS } from './constants';
import { CourseCurriculumTab } from './CourseCurriculumTab';
import { CourseFormFields, type CourseFormValues } from './CourseFormFields';
import { useCourseStore } from './courseStore';
import * as courseApi from './courseApi';
import { USE_API } from '@/lib/api/config';
import type { Course } from './types';

export type CourseTab = 'info' | 'curriculum';

interface Props {
  courseId: string | null;
  initialTab?: CourseTab;
  editable: boolean;
  /** Khóa đang có lớp mở → nhắc đổi học phí chỉ áp dụng lớp mới */
  hasOpenClasses?: boolean;
  onClose: () => void;
}

/** Chi tiết khóa học: Thông tin (xem + sửa) · Lộ trình gắn với khóa */
export function CourseDetailDrawer({ courseId, initialTab = 'info', editable, hasOpenClasses, onClose }: Props) {
  const { isMobile } = useBreakpoint();
  const course = useCourseStore((s) => s.courses.find((c) => c.id === courseId));
  const curriculumCount = useCurriculumStore((s) => s.links.filter((l) => l.courseId === courseId).length);
  const [tab, setTab] = useState<CourseTab>(initialTab);

  useEffect(() => {
    if (courseId) setTab(initialTab);
  }, [courseId, initialTab]);

  const st = course && COURSE_STATUS[course.status];
  const fm = course && FORMATS[course.format];

  return (
    <Drawer
      open={Boolean(courseId)}
      onClose={onClose}
      width={isMobile ? '100%' : 800}
      destroyOnHidden
      styles={{ body: { paddingTop: 0 } }}
      title={
        course &&
        st &&
        fm && (
          <div className="flex min-w-0 flex-col gap-1 pr-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="truncate text-base font-semibold">{course.name}</span>
              <StatusTag tone={st.tone} icon={st.icon} size="sm">
                {st.label}
              </StatusTag>
            </div>
            <span className="text-xs font-normal text-ink-muted">
              <span className="font-mono">{course.code}</span> · {fm.label}
            </span>
          </div>
        )
      }
    >
      {course && (
        <Tabs
          activeKey={tab}
          onChange={(k) => setTab(k as CourseTab)}
          items={[
            {
              key: 'info',
              label: (
                <span className="inline-flex items-center gap-1.5">
                  <Info size={16} aria-hidden /> Thông tin
                </span>
              ),
              children: <InfoTab key={course.id} course={course} editable={editable} hasOpenClasses={hasOpenClasses} />,
            },
            {
              key: 'curriculum',
              label: (
                <span className="inline-flex items-center gap-1.5">
                  <Route size={16} aria-hidden /> Lộ trình ({curriculumCount})
                </span>
              ),
              children: <CourseCurriculumTab course={course} editable={editable} />,
            },
          ]}
        />
      )}
    </Drawer>
  );
}

function InfoTab({ course, editable, hasOpenClasses }: { course: Course; editable: boolean; hasOpenClasses?: boolean }) {
  const [form] = Form.useForm<CourseFormValues>();
  const notify = useNotify();
  const updateCourse = useCourseStore((s) => s.updateCourse);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);

  const save = async (v: CourseFormValues) => {
    setSaving(true);
    const patch = {
      ...v,
      name: v.name.trim(),
      maxStudents: v.format === 'private' ? undefined : v.maxStudents,
      description: v.description?.trim() || undefined,
    };
    try {
      if (USE_API) await courseApi.updateCourse(course.id, { ...course, ...patch });
      else {
        await new Promise((r) => setTimeout(r, 600)); // giả lập gọi API
        updateCourse(course.id, patch);
      }
    } catch (e) {
      notify.error({ title: 'Chưa lưu được khóa học', description: (e as Error).message });
      return;
    } finally {
      setSaving(false);
    }
    setDirty(false);
    notify.success({ title: 'Đã cập nhật khóa học', description: `${v.code} — ${v.name}` });
  };

  return (
    <div className="flex flex-col gap-4">
      {hasOpenClasses && editable && (
        <p className="rounded-lg bg-warning-50 px-3 py-2 text-[13px] text-warning-700">
          Khóa đang có lớp mở — đổi học phí / số buổi chỉ áp dụng cho lớp mở và ghi danh mới.
        </p>
      )}
      <Form
        form={form}
        layout="vertical"
        requiredMark
        disabled={!editable}
        initialValues={course}
        onFinish={save}
        onValuesChange={() => setDirty(true)}
        className="grid grid-cols-1 gap-x-4 md:grid-cols-2"
      >
        <CourseFormFields editingId={course.id} />
        {editable && (
          <div className="sticky bottom-0 -mx-6 flex flex-col-reverse gap-2 border-t border-line bg-surface px-6 py-3 sm:flex-row sm:justify-end md:col-span-2">
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
