import { Checkbox, Form, Radio } from 'antd';
import dayjs from 'dayjs';
import { ModalForm, TextArea } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { LESSON_COVERAGE } from '@/features/academic/classes/constants';
import type { ClassSession, LessonCoverage, SessionLog } from '@/features/academic/classes/types';
import type { Lesson } from '@/features/academic/curriculum/types';
import { useAuth } from '@/hooks/useAuth';
import { fmtDateTime } from '@/lib/date';
import { USE_API } from '@/lib/api/config';
import * as portalApi from '@/features/portal/portalApi';

interface Values {
  coverage: LessonCoverage;
  coveredChapterIds?: string[];
  content: string;
  homework?: string;
  issues?: string;
}

interface Props {
  open: boolean;
  onClose: () => void;
  session: ClassSession;
  lesson?: Lesson;
  /** id GV đang đăng nhập — để ghi giờ ra khi kết thúc */
  teacherId: string;
}

/**
 * "Kết thúc buổi học" = báo cáo tiến độ cho trung tâm.
 *  - Lần đầu: ghi giờ kết thúc + giờ ra (chấm công) của GV, buổi chuyển "Đã dạy"
 *  - Sau đó GV vẫn mở lại để SỬA báo cáo (không khóa)
 */
export function EndSessionModal({ open, onClose, session, lesson, teacherId }: Props) {
  const { user } = useAuth();
  const updateSession = useClassStore((s) => s.updateSession);
  const log = session.teacherLog;
  const editing = Boolean(log?.endedAt);
  const chapters = lesson?.chapters ?? [];

  const submit = async (v: Values) => {
    if (USE_API) {
      // Server ghi báo cáo + tự check-out GV nếu đang check-in
      await portalApi.submitReport(session.id, {
        content: v.content,
        homework: v.homework,
        issues: v.issues,
        coverage: v.coverage,
        coveredChapterIds: v.coverage === 'full' ? chapters.map((c) => c.id) : v.coverage === 'none' ? [] : (v.coveredChapterIds ?? []),
      });
      return;
    }
    const now = new Date().toISOString();
    const teacherLog: SessionLog = {
      ...v,
      // Dạy xong bài → coi như xong hết các phần
      coveredChapterIds: v.coverage === 'full' ? chapters.map((c) => c.id) : v.coverage === 'none' ? [] : v.coveredChapterIds,
      endedAt: log?.endedAt ?? now,
      author: user?.fullName ?? '',
      at: now,
    };
    const hm = dayjs().format('HH:mm');
    const teacherAttendance = editing
      ? session.teacherAttendance
      : session.teacherAttendance?.map((t) => (t.teacherId === teacherId && t.checkIn && !t.checkOut ? { ...t, checkOut: hm, checkedAt: now } : t));
    updateSession(session.id, {
      teacherLog,
      teacherAttendance,
      status: session.status === 'scheduled' ? 'completed' : session.status,
    });
  };

  return (
    <ModalForm<Values>
      key={`${session.id}-${open}`}
      open={open}
      onClose={onClose}
      width={620}
      title={editing ? 'Sửa báo cáo tiến độ buổi học' : 'Kết thúc buổi học'}
      description={
        editing
          ? `Kết thúc lúc ${fmtDateTime(log!.endedAt!)} · sửa lần cuối ${fmtDateTime(log!.at)} — giáo vụ thấy bản mới nhất.`
          : 'Cập nhật tiến độ để nhân viên trung tâm nắm tình hình lớp. Bạn vẫn sửa lại được sau khi lưu.'
      }
      submitAction="save"
      submitText={editing ? 'Lưu báo cáo' : 'Kết thúc & gửi báo cáo'}
      successMessage={editing ? 'Đã cập nhật báo cáo tiến độ' : 'Đã kết thúc buổi học — báo cáo đã gửi giáo vụ'}
      initialValues={{
        coverage: log?.coverage ?? 'full',
        coveredChapterIds: log?.coveredChapterIds ?? chapters.map((c) => c.id),
        content: log?.content,
        homework: log?.homework,
        issues: log?.issues,
      }}
      onSubmit={submit}
    >
      <Form.Item name="coverage" label="Tiến độ so với nội dung bài" rules={[{ required: true }]}>
        <Radio.Group className="flex flex-col gap-1.5">
          {(Object.keys(LESSON_COVERAGE) as LessonCoverage[]).map((k) => (
            <Radio key={k} value={k}>
              <span className="font-medium">{LESSON_COVERAGE[k].label}</span>
              <span className="ml-1.5 text-xs text-ink-muted">{LESSON_COVERAGE[k].hint}</span>
            </Radio>
          ))}
        </Radio.Group>
      </Form.Item>

      {chapters.length > 0 && (
        <Form.Item noStyle shouldUpdate={(a, b) => a.coverage !== b.coverage}>
          {({ getFieldValue }) =>
            getFieldValue('coverage') === 'partial' && (
              <Form.Item name="coveredChapterIds" label="Đã dạy xong các phần" extra="Phần chưa đánh dấu → giáo vụ biết cần dạy tiếp ở buổi sau">
                <Checkbox.Group className="flex flex-col gap-1" options={chapters.map((c) => ({ value: c.id, label: c.title }))} />
              </Form.Item>
            )
          }
        </Form.Item>
      )}

      <Form.Item name="content" label="Đã dạy gì, lớp tiếp thu thế nào" rules={[{ required: true, message: 'Nhập nội dung đã dạy' }]}>
        <TextArea rows={3} maxLength={1000} placeholder="Vd: Hoàn thành mẫu ～ば, lớp nắm tốt; phần luyện nghe còn chậm." />
      </Form.Item>
      <Form.Item name="homework" label="Bài tập về nhà đã dặn">
        <TextArea rows={2} maxLength={500} />
      </Form.Item>
      <Form.Item name="issues" label="Cần trung tâm hỗ trợ" extra="Giáo vụ nhận được ngay (HV vắng nhiều, máy chiếu hỏng...)">
        <TextArea rows={2} maxLength={500} />
      </Form.Item>
    </ModalForm>
  );
}
